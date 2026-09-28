// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheBleedingDeacons.Freedom.Client.Crypto;

namespace TheBleedingDeacons.Freedom.Tests.Support;

/// <summary>
/// The Freedom plugin's REST surface, in memory, keeping the rules the client
/// depends on: versions from one counter that never repeats, a complete
/// manifest with an ETag, secrets sealed per request to the key the device
/// enrolled with, and the plugin's refusal codes.
/// </summary>
/// <remarks>
/// Deliberately the server's rules and not the client's: a fake that agreed
/// with whatever the client assumed would prove nothing. Where the two could
/// disagree — the envelope, the field names — the plugin's own tests and the
/// committed fixtures are what keep them honest.
/// </remarks>
public sealed class FakeFreedomServer : HttpMessageHandler
{
	public const string BaseUrl = "https://intergroup.example.org";
	public const string Code = "one-time-code";
	public const string LinkSession = "fdt_link-session";

	private readonly Dictionary<string, (string Value, long Version, bool Secret)> _values = new(StringComparer.Ordinal);
	private string? _challenge;
	private int _tokens;

	public long Revision { get; private set; }

	public string? Token { get; private set; }

	public string? PublicKey { get; private set; }

	public bool KnownDevice { get; set; }

	public bool Offline { get; set; }

	public bool Revoked { get; set; }

	public bool Disabled { get; set; }

	/// <summary>A 403 code to refuse every authenticated request with, e.g. <c>freedom_not_authorised</c>.</summary>
	public string? Refusal { get; set; }

	/// <summary>A status to answer every manifest request with, e.g. 500 or 429.</summary>
	public HttpStatusCode? ManifestStatus { get; set; }

	/// <summary>A status to answer every values request with.</summary>
	public HttpStatusCode? ValuesStatus { get; set; }

	/// <summary>A refusal to send the browser back with instead of a code.</summary>
	public string? BrowserRefusal { get; set; }

	public bool AcceptSessions { get; set; } = true;

	/// <summary>Seal secrets to this key instead of the enrolled one — a device whose key no longer matches.</summary>
	public string? SealTo { get; set; }

	public int KeyFaults { get; private set; }

	/// <summary>The path the site lives under, from the last request — empty for a site at the root.</summary>
	public string SitePath { get; private set; } = string.Empty;

	public List<(HttpMethod Method, string Path, string? Body, string? IfNoneMatch)> Requests { get; } = [];

	public int Count(string path) => Requests.Count(r => string.Equals(r.Path, path, StringComparison.Ordinal));

	public IReadOnlyList<string> RequestedKeys => Requests
		.Where(r => string.Equals(r.Path, "config/values", StringComparison.Ordinal) && r.Body is not null)
		.SelectMany(r => JsonNode.Parse(r.Body!)!["keys"]!.AsArray().Select(k => k!.GetValue<string>()))
		.ToList();

	public void Set(string key, string value, bool secret = false) => _values[key] = (value, ++Revision, secret);

	public void Remove(string key) => _values.Remove(key);

	public long VersionOf(string key) => _values[key].Version;

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
		var absolute = request.RequestUri!.AbsolutePath;
		var at = absolute.IndexOf("/wp-json/freedom/v1/", StringComparison.Ordinal);
		var path = at < 0 ? absolute : absolute[(at + "/wp-json/freedom/v1/".Length)..];
		SitePath = at < 0 ? string.Empty : absolute[..at];

		// The plugin accepts the ETag from If-None-Match or ?etag=, as the
		// client sends both; this answers to either, header first.
		var ifNoneMatch = request.Headers.IfNoneMatch.FirstOrDefault()?.Tag.Trim('"')
			?? System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["etag"];
		Requests.Add((request.Method, path, body, ifNoneMatch));

		if (Offline)
		{
			throw new HttpRequestException("No route to host");
		}

		var bearer = request.Headers.Authorization?.Parameter;

		return path switch
		{
			"auth/start" => Start(request.RequestUri),
			"auth/exchange" => Exchange(JsonNode.Parse(body!)!),
			"auth/session" => Session(bearer, JsonNode.Parse(body!)!),
			"config/manifest" => Authenticated(bearer) ?? Manifest(ifNoneMatch),
			"config/values" => Authenticated(bearer) ?? Values(JsonNode.Parse(body!)!),
			"tablet/key-fault" => Authenticated(bearer) ?? KeyFault(),
			"tablet" => Authenticated(bearer) ?? SignOut(),
			_ => Error(HttpStatusCode.NotFound, "rest_no_route", "No route was found matching the URL and request method."),
		};
	}

	/// <summary>Where the fake browser should come back to after <c>auth/start</c>.</summary>
	public string BrowserOutcome() => BrowserRefusal is null ? "code=" + Code : "error=" + BrowserRefusal;

	private HttpResponseMessage Start(Uri uri)
	{
		var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
		_challenge = query["code_challenge"];

		return Json(HttpStatusCode.OK, new { state = "state-1", authorization_url = "https://accounts.example.org/authorize?state=state-1" });
	}

	private HttpResponseMessage Exchange(JsonNode body)
	{
		if (!string.Equals(body["code"]?.GetValue<string>(), Code, StringComparison.Ordinal))
		{
			return Error(HttpStatusCode.BadRequest, "freedom_bad_code", "That sign-in code has expired or has already been used.");
		}

		var verifier = body["code_verifier"]?.GetValue<string>() ?? string.Empty;
		if (_challenge is null || !string.Equals(Pkce.ChallengeFor(verifier), _challenge, StringComparison.Ordinal))
		{
			return Error(HttpStatusCode.BadRequest, "freedom_bad_verifier", "That sign-in was not started by this app.");
		}

		return Enrol(body);
	}

	private HttpResponseMessage Session(string? bearer, JsonNode body)
	{
		if (!string.Equals(bearer, LinkSession, StringComparison.Ordinal))
		{
			return Error(HttpStatusCode.Unauthorized, "freedom_unauthenticated", "That Link session is not signed in.");
		}

		if (!AcceptSessions)
		{
			return Error(HttpStatusCode.Forbidden, "freedom_sessions_not_accepted", "This application does not accept a Link session.");
		}

		return Enrol(body);
	}

	private HttpResponseMessage Enrol(JsonNode body)
	{
		if (Refusal is not null)
		{
			return Error(HttpStatusCode.Forbidden, Refusal, "This account may not use this application.");
		}

		PublicKey = body["public_key"]!.GetValue<string>();
		Token = "frt_" + (++_tokens).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(64, '0');
		Revoked = false;

		var reattached = KnownDevice;
		KnownDevice = true;

		return Json(reattached ? HttpStatusCode.OK : HttpStatusCode.Created, new
		{
			token = Token,
			tablet = new { id = 12, label = body["label"]?.GetValue<string>() ?? string.Empty, created_at = 1_790_000_000, reattached },
			application = new { slug = "register", name = "Register" },
		});
	}

	private HttpResponseMessage? Authenticated(string? bearer)
	{
		if (Token is null || Revoked || !string.Equals(bearer, Token, StringComparison.Ordinal))
		{
			return Error(HttpStatusCode.Unauthorized, "freedom_unauthenticated", "This tablet is not signed in.");
		}

		if (Disabled)
		{
			return Error(HttpStatusCode.Forbidden, "freedom_application_disabled", "This application is not currently available.");
		}

		if (Refusal is not null)
		{
			return Error(HttpStatusCode.Forbidden, Refusal, "This account may no longer use this application.");
		}

		return null;
	}

	private HttpResponseMessage Manifest(string? ifNoneMatch)
	{
		if (ManifestStatus is { } status)
		{
			return Error(status, "freedom_unavailable", "Configuration is temporarily unavailable.");
		}

		var keys = _values.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => new { key = v.Key, version = v.Value.Version, secret = v.Value.Secret }).ToList();
		var etag = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', keys.Select(k => $"{k.key}:{k.version}:{(k.secret ? 1 : 0)}")))))[..32];

		if (string.Equals(ifNoneMatch, etag, StringComparison.Ordinal))
		{
			return new HttpResponseMessage(HttpStatusCode.NotModified);
		}

		return Json(HttpStatusCode.OK, new { application = "register", tablet = 12, etag, checked_at = 1_790_000_000, keys });
	}

	private HttpResponseMessage Values(JsonNode body)
	{
		if (ValuesStatus is { } status)
		{
			return Error(status, "freedom_unavailable", "Configuration is temporarily unavailable.");
		}

		var values = new List<object>();
		var missing = new List<string>();

		foreach (var key in body["keys"]!.AsArray().Select(k => k!.GetValue<string>()))
		{
			if (!_values.TryGetValue(key, out var stored))
			{
				missing.Add(key);
				continue;
			}

			if (!stored.Secret)
			{
				values.Add(new { key, version = stored.Version, secret = false, value = stored.Value });
				continue;
			}

			var (k, p) = Sealing.Seal(key, stored.Version, stored.Value, SealTo ?? PublicKey!);
			values.Add(new { key, version = stored.Version, secret = true, k, p });
		}

		return Json(HttpStatusCode.OK, new { values, missing, unreadable = Array.Empty<string>() });
	}

	private HttpResponseMessage KeyFault()
	{
		KeyFaults++;
		return Json(HttpStatusCode.OK, new { recorded = true });
	}

	private HttpResponseMessage SignOut()
	{
		Revoked = true;
		return Json(HttpStatusCode.OK, new { revoked = true });
	}

	private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status)
	{
		Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
	};

	private static HttpResponseMessage Error(HttpStatusCode status, string code, string message) =>
		Json(status, new { code, message, data = new { status = (int)status } });
}
