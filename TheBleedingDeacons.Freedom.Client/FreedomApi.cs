// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TheBleedingDeacons.Freedom.Client.Serialization;
using TheBleedingDeacons.Freedom.Models;

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// The Freedom plugin's REST routes, one method each. Returns answers; never
/// throws for a network failure or an error status.
/// </summary>
/// <remarks>
/// <para>Most apps want <see cref="FreedomClient"/>, which drives this. It is
/// public for an app that wants to do something the client does not.</para>
/// <para>Headers are set per request rather than on the <see cref="HttpClient"/>,
/// so a shared client is safe — integrity-sharp's arrangement. The namespace is
/// appended to the base URL, never configured: <c>{base}/wp-json/freedom/v1/…</c>.</para>
/// <para><b>Retries are few, on purpose.</b> This runs at app start, and a
/// start that waits half a minute on a server that is down is worse than one
/// that carries on with what it has. Reads retry twice on a network error, a
/// 5xx or a 429; nothing that writes is retried.</para>
/// <para><b>A firewall's 403 is retried, a plugin's is not.</b> SiteGround's
/// firewall in front of aa-bristol.org fingerprints the TLS handshake, and
/// refuses the <i>first</i> full handshake .NET's managed handler makes in a
/// process with an HTML 403 page; later connections resume the session and
/// pass. Checked 2026-09-28, deterministically: request 1 403, requests 2 and 3
/// through. WinHTTP and Android's native handler are not refused at all. So a
/// 403 whose body is not JSON is the firewall, the request never reached
/// WordPress, and it is retried — even a write, since nothing was spent. A
/// JSON 403 is the plugin refusing and is never retried. integrity-sharp
/// arrived at the same rule for the same host.</para>
/// <para><b>Every request carries a <c>User-Agent</c></b>, since .NET sends none
/// of its own: the traffic is attributable in an access log, as Link's and
/// Fellowship's are. A client that already sets one is left alone.</para>
/// </remarks>
public sealed class FreedomApi : IDisposable
{
	/// <summary>The wire's JSON: snake_case, as WordPress writes it.</summary>
	internal static readonly JsonSerializerOptions Json = FreedomJsonContext.Default.Options;

	private const int MaxReadRetries = 2;

	/// <summary>Retries after a firewall's HTML 403, for any request.</summary>
	private const int MaxFirewallRetries = 2;

	private readonly Uri _root;
	private readonly HttpClient _http;
	private readonly bool _ownsHttp;
	private readonly ILogger _logger;
	private readonly TimeSpan _retryDelay;
	private readonly string _userAgent;

	/// <summary>
	/// Initializes a new instance of the <see cref="FreedomApi"/> class.
	/// </summary>
	/// <param name="baseUrl">The site's address. HTTPS unless <paramref name="allowInsecureBaseUrl"/>.</param>
	/// <param name="httpClient">A client to share; one is created and owned otherwise.</param>
	/// <param name="logger">A logger. Values and tokens are never logged.</param>
	/// <param name="allowInsecureBaseUrl">Allow plain http, for a local development site.</param>
	/// <param name="retryDelay">The pause before a retry; half a second by default.</param>
	/// <param name="userAgent">The <c>User-Agent</c> to send; <see cref="DefaultUserAgent"/> when null.</param>
	public FreedomApi(Uri baseUrl, HttpClient? httpClient = null, ILogger? logger = null, bool allowInsecureBaseUrl = false, TimeSpan? retryDelay = null, string? userAgent = null)
	{
		ArgumentNullException.ThrowIfNull(baseUrl);

		if (!FreedomOptions.IsAllowedBaseUrl(baseUrl, allowInsecureBaseUrl))
		{
			throw new ArgumentException("The base URL must be an absolute https address.", nameof(baseUrl));
		}

		_root = new Uri(baseUrl.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/wp-json/freedom/v1/");
		_http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
		_ownsHttp = httpClient is null;
		_logger = logger ?? NullLogger.Instance;
		_retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(500);
		_userAgent = string.IsNullOrWhiteSpace(userAgent) ? DefaultUserAgent(null) : userAgent;
	}

	/// <summary>
	/// The User-Agent sent when none is given:
	/// <c>Freedom.Client/0.1.0 (register; Microsoft Windows 10.0.26200; +https://github.com/bleedingdeacons/freedom-sharp)</c>.
	/// </summary>
	public static string DefaultUserAgent(string? application)
	{
		var version = typeof(FreedomApi).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";
		var plus = version.IndexOf('+', StringComparison.Ordinal);
		if (plus >= 0)
		{
			// Source Link appends the commit; the version alone is what a log needs.
			version = version[..plus];
		}

		var who = string.IsNullOrWhiteSpace(application) ? string.Empty : application + "; ";

		return $"Freedom.Client/{version} ({who}{RuntimeInformation.OSDescription.Trim()}; +https://github.com/bleedingdeacons/freedom-sharp)";
	}

	/// <summary>Begin a browser sign-in.</summary>
	public Task<ApiResponse<SignInStart>> StartSignInAsync(string application, Uri callbackUri, string codeChallenge, string provider, CancellationToken cancellationToken = default)
	{
		var query = "auth/start?application=" + Uri.EscapeDataString(application)
			+ "&redirect_uri=" + Uri.EscapeDataString(callbackUri.OriginalString)
			+ "&code_challenge=" + Uri.EscapeDataString(codeChallenge)
			+ "&provider=" + Uri.EscapeDataString(provider);

		return SendAsync<SignInStart>(() => new HttpRequestMessage(HttpMethod.Get, new Uri(_root, query)), retry: false, cancellationToken);
	}

	/// <summary>Spend the one-time code from a browser sign-in.</summary>
	public Task<ApiResponse<Enrolment>> ExchangeAsync(string application, string code, string codeVerifier, Abstractions.DeviceIdentity device, string publicKey, CancellationToken cancellationToken = default)
	{
		var body = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["application"] = application,
			["code"] = code,
			["code_verifier"] = codeVerifier,
			["device_id"] = device.DeviceId,
			["public_key"] = publicKey,
			["label"] = device.Label,
			["platform"] = device.Platform,
			["model"] = device.Model,
			["app_version"] = device.AppVersion,
		};

		return SendAsync<Enrolment>(() => Post("auth/exchange", body, token: null), retry: false, cancellationToken);
	}

	/// <summary>Sign in with a session the app already holds with this site, in place of a browser sign-in.</summary>
	public Task<ApiResponse<Enrolment>> HandOverSessionAsync(string sessionToken, string application, string publicKey, Abstractions.DeviceIdentity? device, CancellationToken cancellationToken = default)
	{
		var body = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["application"] = application,
			["public_key"] = publicKey,
		};

		if (device is not null)
		{
			body["label"] = device.Label;
			body["platform"] = device.Platform;
			body["model"] = device.Model;
			body["app_version"] = device.AppVersion;
		}

		return SendAsync<Enrolment>(() => Post("auth/session", body, sessionToken), retry: false, cancellationToken);
	}

	/// <summary>Every key this device should hold. Sends <paramref name="etag"/> as If-None-Match, so an unchanged manifest is a 304.</summary>
	public Task<ApiResponse<Manifest>> GetManifestAsync(string token, string? etag, CancellationToken cancellationToken = default)
	{
		return SendAsync<Manifest>(
			() =>
			{
				// The ETag travels twice. If-None-Match is the standard way, and
				// SiteGround's proxy does not pass it through to WordPress (checked
				// 2026-09-28: the right ETag, sent back, still got a 200), so it
				// goes in the query as well, which nothing in between touches.
				var path = string.IsNullOrEmpty(etag) ? "config/manifest" : "config/manifest?etag=" + Uri.EscapeDataString(etag);
				var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_root, path));
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
				if (!string.IsNullOrEmpty(etag))
				{
					request.Headers.TryAddWithoutValidation("If-None-Match", "\"" + etag + "\"");
				}

				return request;
			},
			retry: true,
			cancellationToken);
	}

	/// <summary>The values for up to 100 keys.</summary>
	public Task<ApiResponse<ValuesResponse>> GetValuesAsync(string token, IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
	{
		// A read, though it is a POST: the keys travel in the body rather than a
		// query string that proxies log.
		return SendAsync<ValuesResponse>(() => Post("config/values", new ValuesRequest(keys), token), retry: true, cancellationToken);
	}

	/// <summary>Tell the server this device was sent a secret it cannot open.</summary>
	public Task<ApiResponse<JsonElementBox>> ReportKeyFaultAsync(string token, CancellationToken cancellationToken = default)
	{
		return SendAsync<JsonElementBox>(() => Post("tablet/key-fault", new Dictionary<string, string>(StringComparer.Ordinal), token), retry: false, cancellationToken);
	}

	/// <summary>Sign this device out.</summary>
	public Task<ApiResponse<JsonElementBox>> SignOutAsync(string token, CancellationToken cancellationToken = default)
	{
		return SendAsync<JsonElementBox>(
			() =>
			{
				var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(_root, "tablet"));
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

				return request;
			},
			retry: false,
			cancellationToken);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_ownsHttp)
		{
			_http.Dispose();
		}
	}

	private HttpRequestMessage Post(string path, object body, string? token)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_root, path))
		{
			Content = JsonContent.Create(body, options: Json),
		};

		if (token is not null)
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}

		return request;
	}

	private async Task<ApiResponse<T>> SendAsync<T>(Func<HttpRequestMessage> build, bool retry, CancellationToken cancellationToken)
		where T : class
	{
		var attempts = retry ? MaxReadRetries + 1 : 1;

		for (var attempt = 1; ; attempt++)
		{
			using var request = build();
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			// A connection per request. The firewall's refusal is of a
			// connection's handshake, and a retry over the same pooled
			// connection is refused again; a new one resumes the TLS session
			// and gets through. Freedom makes a handful of requests per start,
			// so keep-alive buys nothing worth that.
			request.Headers.ConnectionClose = true;
			if (request.Headers.UserAgent.Count == 0 && _http.DefaultRequestHeaders.UserAgent.Count == 0)
			{
				request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
			}

			HttpResponseMessage response;
			try
			{
				response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception e) when (e is HttpRequestException || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
			{
				_logger.LogWarning("Freedom: {Method} {Path} could not reach the server ({Reason})", request.Method, request.RequestUri?.AbsolutePath, e.GetType().Name);

				if (attempt < attempts)
				{
					await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
					continue;
				}

				return new ApiResponse<T> { Success = false };
			}

			using (response)
			{
				var status = response.StatusCode;
				var transient = (int)status >= 500 || status == HttpStatusCode.TooManyRequests;

				if (status == HttpStatusCode.Forbidden && IsFirewallPage(response) && attempt <= MaxFirewallRetries)
				{
					_logger.LogWarning("Freedom: {Method} {Path} was refused by the site's firewall, not the plugin; retrying", request.Method, request.RequestUri?.AbsolutePath);
					await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
					continue;
				}

				if (transient && attempt < attempts)
				{
					_logger.LogWarning("Freedom: {Method} {Path} answered {Status}; retrying", request.Method, request.RequestUri?.AbsolutePath, (int)status);
					await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
					continue;
				}

				if (status == HttpStatusCode.NotModified)
				{
					return new ApiResponse<T> { Success = true, StatusCode = status };
				}

				if (response.IsSuccessStatusCode)
				{
					try
					{
						var data = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
						return new ApiResponse<T> { Success = data is not null, Data = data, StatusCode = status };
					}
					catch (Exception e) when (e is JsonException or NotSupportedException)
					{
						_logger.LogWarning("Freedom: {Method} {Path} answered something that is not the expected JSON", request.Method, request.RequestUri?.AbsolutePath);
						return new ApiResponse<T> { Success = false, StatusCode = status, Error = new ApiError { Code = "freedom_bad_response", Message = "The server's answer could not be read." } };
					}
				}

				var error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
				_logger.LogInformation("Freedom: {Method} {Path} answered {Status} {Code}", request.Method, request.RequestUri?.AbsolutePath, (int)status, error.Code);

				return new ApiResponse<T> { Success = false, StatusCode = status, Error = error };
			}
		}
	}

	/// <summary>
	/// Whether a 403 is a web server's or firewall's page rather than the
	/// plugin's JSON refusal. integrity-sharp's rule: no Content-Type is not
	/// assumed to be a firewall, because a genuine refusal served without one
	/// must not be retried.
	/// </summary>
	private static bool IsFirewallPage(HttpResponseMessage response)
	{
		var mediaType = response.Content.Headers.ContentType?.MediaType;

		return !string.IsNullOrEmpty(mediaType) && !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		try
		{
			using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
			var root = document.RootElement;

			if (root.ValueKind == JsonValueKind.Object
				&& root.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
				&& root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
			{
				return new ApiError { Code = code.GetString()!, Message = message.GetString()! };
			}
		}
		catch (JsonException)
		{
			// A firewall's HTML page, typically. Reported below as what it is.
		}

		return new ApiError { Code = "http_" + (int)response.StatusCode, Message = response.ReasonPhrase ?? "The server refused the request." };
	}
}
