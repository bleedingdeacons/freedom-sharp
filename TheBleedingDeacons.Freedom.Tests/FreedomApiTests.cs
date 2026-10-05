// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Net;
using TheBleedingDeacons.Freedom.Client;
using TheBleedingDeacons.Freedom.Tests.Support;

namespace TheBleedingDeacons.Freedom.Tests;

/// <summary>
/// The low-level client: addresses, headers, retries, and what an error looks
/// like when it comes back.
/// </summary>
public sealed class FreedomApiTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public void PlainHttpIsRefusedUnlessAskedFor()
	{
		Assert.Throws<ArgumentException>(() => new FreedomApi(new Uri("http://example.org")));
		using var allowed = new FreedomApi(new Uri("http://example.org"), allowInsecureBaseUrl: true);
	}

	[Fact]
	public async Task TheNamespaceIsAppendedNotConfigured()
	{
		// A site in a subdirectory — the test environment lives under one —
		// keeps its path; the REST namespace goes after it.
		using var server = new FakeFreedomServer();
		using var api = Api(server, "https://intergroup.example.org/amber/");

		await api.StartSignInAsync("register", new Uri("org.example.register.freedom://auth"), "c", "google", Ct);

		Assert.Equal("auth/start", server.Requests[0].Path);
		Assert.Equal("/amber", server.SitePath);
	}

	[Fact]
	public async Task TheManifestSendsItsEtagBackAndA304IsNotAnError()
	{
		using var server = new FakeFreedomServer();
		using var harness = new Harness();
		server.Set("smtp.host", "mail");
		using var api = Api(server);
		var token = await EnrolAsync(server, api);

		var first = await api.GetManifestAsync(token, null, Ct);
		var second = await api.GetManifestAsync(token, first.Data!.Etag, Ct);

		Assert.True(first.Success);
		Assert.Equal(first.Data.Etag, server.Requests[^1].IfNoneMatch);
		Assert.True(second.Success);
		Assert.True(second.NotModified);
		Assert.Null(second.Data);
	}

	[Fact]
	public async Task TheEtagAlsoTravelsInTheQueryForProxiesThatDropTheHeader()
	{
		var handler = new SequenceHandler(HttpStatusCode.OK) { Body = "{}" };
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		await api.GetManifestAsync("t", "abc123", Ct);
		Assert.EndsWith("config/manifest?etag=abc123", handler.LastUri!.ToString(), StringComparison.Ordinal);

		await api.GetManifestAsync("t", null, Ct);
		Assert.EndsWith("config/manifest", handler.LastUri!.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task AReadIsRetriedOnAServerErrorAndAWriteIsNot()
	{
		var handler = new SequenceHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable);
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		var read = await api.GetManifestAsync("t", null, Ct);
		Assert.Equal(3, handler.Calls);
		Assert.False(read.Success);
		Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);

		handler.Calls = 0;
		await api.SignOutAsync("t", Ct);
		Assert.Equal(1, handler.Calls);
	}

	[Fact]
	public async Task ARetryThatSucceedsIsASuccess()
	{
		var handler = new SequenceHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		var read = await api.GetValuesAsync("t", ["a"], Ct);

		Assert.True(read.Success);
		Assert.Equal(2, handler.Calls);
	}

	[Fact]
	public async Task NoNetworkIsUnreachableNotAnException()
	{
		using var server = new FakeFreedomServer { Offline = true };
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(server), retryDelay: TimeSpan.Zero);

		var read = await api.GetManifestAsync("t", null, Ct);

		Assert.True(read.Unreachable);
		Assert.Null(read.StatusCode);
		Assert.Equal(3, server.Requests.Count);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task AConnectionDroppedAsAndroidReportsItIsUnreachableNotAnException(bool asAndroid)
	{
		// AndroidMessageHandler throws WebException, not HttpRequestException,
		// when the connection drops — as it does when the app is backgrounded
		// mid-request. Seen on the Register tablet 2026-10-04, escaping a sync.
		Exception thrown = asAndroid ? FakeFreedomServer.ConnectionAborted() : new IOException("Broken pipe");
		var handler = new ThrowingHandler(thrown);
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		var read = await api.GetValuesAsync("t", ["smtp.host"], Ct);
		Assert.True(read.Unreachable);
		Assert.False(read.Success);
		Assert.Equal(3, handler.Calls);

		handler.Calls = 0;
		var write = await api.ReportKeyFaultAsync("t", Ct);
		Assert.True(write.Unreachable);
		Assert.Equal(1, handler.Calls);
	}

	[Fact]
	public async Task ACallersOwnCancellationIsNotMistakenForNoNetwork()
	{
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();
		var handler = new ThrowingHandler(new TaskCanceledException());
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.GetManifestAsync("t", null, cancelled.Token));
	}

	[Fact]
	public async Task AnErrorComesBackInTheServersWords()
	{
		using var server = new FakeFreedomServer();
		using var api = Api(server);

		var response = await api.GetManifestAsync("frt_invented", null, Ct);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		Assert.Equal("freedom_unauthenticated", response.Error!.Code);
		Assert.Equal("This tablet is not signed in.", response.Error.Message);
	}

	[Fact]
	public async Task AFirewallsHtmlPageIsRetriedAndThenReportedAsWhatItIs()
	{
		var handler = new SequenceHandler(HttpStatusCode.Forbidden) { Body = "<html>Blocked</html>", MediaType = "text/html" };
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		var response = await api.GetManifestAsync("t", null, Ct);

		Assert.Equal(3, handler.Calls);
		Assert.Equal("http_403", response.Error!.Code);
	}

	[Fact]
	public async Task AFirewallRefusingTheFirstHandshakeIsNotAnError()
	{
		// SiteGround refuses the first TLS handshake .NET's managed handler
		// makes in a process; the retry resumes the session and gets through.
		// Writes too: the request never reached WordPress, so nothing was spent.
		var handler = new SequenceHandler(HttpStatusCode.Forbidden, HttpStatusCode.OK) { Body = "<html>Blocked</html>", MediaType = "text/html" };
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		var response = await api.SignOutAsync("t", Ct);

		Assert.Equal(2, handler.Calls);
		Assert.True(response.Success);
	}

	[Fact]
	public async Task ThePluginsOwnRefusalIsNeverRetried()
	{
		using var server = new FakeFreedomServer();
		using var api = Api(server);

		await api.GetManifestAsync("frt_invented", null, Ct);

		Assert.Single(server.Requests);
	}

	[Fact]
	public async Task AnAnswerThatIsNotJsonIsNotASuccess()
	{
		var handler = new SequenceHandler(HttpStatusCode.OK) { Body = "not json" };
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		var response = await api.GetManifestAsync("t", null, Ct);

		Assert.False(response.Success);
		Assert.Equal("freedom_bad_response", response.Error!.Code);
	}

	[Fact]
	public async Task TheTokenTravelsAsABearerAndNeverInTheAddress()
	{
		var handler = new SequenceHandler(HttpStatusCode.OK) { Body = "{}" };
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		await api.GetValuesAsync("frt_secret", ["smtp.password"], Ct);

		Assert.Equal("Bearer frt_secret", handler.LastAuthorization);
		Assert.DoesNotContain("frt_secret", handler.LastUri!.ToString(), StringComparison.Ordinal);
		Assert.DoesNotContain("smtp.password", handler.LastUri.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task EveryRequestSaysWhatItIs()
	{
		// SiteGround's firewall answers a .NET request with no User-Agent with
		// a bare 403 page; .NET sends none of its own.
		var handler = new SequenceHandler(HttpStatusCode.OK) { Body = "{}" };
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		await api.GetManifestAsync("t", null, Ct);

		Assert.StartsWith("Freedom.Client/", handler.LastUserAgent, StringComparison.Ordinal);
		Assert.Contains("github.com/bleedingdeacons/freedom-sharp", handler.LastUserAgent, StringComparison.Ordinal);
	}

	[Fact]
	public async Task AnAppsOwnUserAgentIsUsed()
	{
		var given = new SequenceHandler(HttpStatusCode.OK) { Body = "{}" };
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(given), retryDelay: TimeSpan.Zero, userAgent: "Register/1.3.0 (Android)");
		await api.GetManifestAsync("t", null, Ct);

		var shared = new SequenceHandler(HttpStatusCode.OK) { Body = "{}" };
		using var client = new HttpClient(shared);
		client.DefaultRequestHeaders.UserAgent.ParseAdd("Link/1.7.0");
		using var api2 = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), client, retryDelay: TimeSpan.Zero);
		await api2.GetManifestAsync("t", null, Ct);

		Assert.Equal("Register/1.3.0 (Android)", given.LastUserAgent);
		Assert.Equal("Link/1.7.0", shared.LastUserAgent);
	}

	[Fact]
	public void TheDefaultNamesTheApplication()
	{
		Assert.Contains("(register; ", FreedomApi.DefaultUserAgent("register"), StringComparison.Ordinal);
		Assert.DoesNotContain("+", FreedomApi.DefaultUserAgent(null).Split(' ')[0], StringComparison.Ordinal);
	}

	private static FreedomApi Api(FakeFreedomServer server, string baseUrl = FakeFreedomServer.BaseUrl) =>
		new(new Uri(baseUrl), new HttpClient(server, disposeHandler: false), retryDelay: TimeSpan.Zero);

	private static async Task<string> EnrolAsync(FakeFreedomServer server, FreedomApi api)
	{
		var verifier = TheBleedingDeacons.Freedom.Client.Crypto.Pkce.NewVerifier();
		await api.StartSignInAsync("register", new Uri("org.example.register.freedom://auth"), TheBleedingDeacons.Freedom.Client.Crypto.Pkce.ChallengeFor(verifier), "google", Ct);
		var enrolled = await api.ExchangeAsync("register", FakeFreedomServer.Code, verifier, new("9774d56d682e549c", "android", "t", "m", "1"), TheBleedingDeacons.Freedom.Client.Crypto.TabletKeyPair.Generate().PublicKey, Ct);

		return enrolled.Data!.Token;
	}

	private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
	{
		public int Calls { get; set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Calls++;

			throw exception;
		}
	}

	private sealed class SequenceHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
	{
		public int Calls { get; set; }

		public string Body { get; init; } = "{\"code\":\"freedom_unavailable\",\"message\":\"Down.\"}";

		public string MediaType { get; init; } = "application/json";

		public string? LastAuthorization { get; private set; }

		public Uri? LastUri { get; private set; }

		public string? LastUserAgent { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			LastAuthorization = request.Headers.Authorization?.ToString();
			LastUri = request.RequestUri;
			LastUserAgent = request.Headers.UserAgent.ToString();
			var status = statuses[Math.Min(Calls, statuses.Length - 1)];
			Calls++;

			return Task.FromResult(new HttpResponseMessage(status)
			{
				Content = status == HttpStatusCode.OK && MediaType.Contains("html", StringComparison.Ordinal)
					? new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
					: new StringContent(status == HttpStatusCode.OK && Body.StartsWith("{\"code\"", StringComparison.Ordinal) ? "{}" : Body, System.Text.Encoding.UTF8, MediaType),
			});
		}
	}
}
