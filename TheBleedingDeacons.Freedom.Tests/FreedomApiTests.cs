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
	public async Task AFirewallsHtmlPageIsReportedAsWhatItIs()
	{
		var handler = new SequenceHandler(HttpStatusCode.Forbidden) { Body = "<html>Blocked</html>", MediaType = "text/html" };
		using var api = new FreedomApi(new Uri(FakeFreedomServer.BaseUrl), new HttpClient(handler), retryDelay: TimeSpan.Zero);

		var response = await api.GetManifestAsync("t", null, Ct);

		Assert.Equal("http_403", response.Error!.Code);
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

	private static FreedomApi Api(FakeFreedomServer server, string baseUrl = FakeFreedomServer.BaseUrl) =>
		new(new Uri(baseUrl), new HttpClient(server, disposeHandler: false), retryDelay: TimeSpan.Zero);

	private static async Task<string> EnrolAsync(FakeFreedomServer server, FreedomApi api)
	{
		var verifier = TheBleedingDeacons.Freedom.Client.Crypto.Pkce.NewVerifier();
		await api.StartSignInAsync("register", new Uri("org.example.register.freedom://auth"), TheBleedingDeacons.Freedom.Client.Crypto.Pkce.ChallengeFor(verifier), "google", Ct);
		var enrolled = await api.ExchangeAsync("register", FakeFreedomServer.Code, verifier, new("9774d56d682e549c", "android", "t", "m", "1"), TheBleedingDeacons.Freedom.Client.Crypto.TabletKeyPair.Generate().PublicKey, Ct);

		return enrolled.Data!.Token;
	}

	private sealed class SequenceHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
	{
		public int Calls { get; set; }

		public string Body { get; init; } = "{\"code\":\"freedom_unavailable\",\"message\":\"Down.\"}";

		public string MediaType { get; init; } = "application/json";

		public string? LastAuthorization { get; private set; }

		public Uri? LastUri { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			LastAuthorization = request.Headers.Authorization?.ToString();
			LastUri = request.RequestUri;
			var status = statuses[Math.Min(Calls, statuses.Length - 1)];
			Calls++;

			return Task.FromResult(new HttpResponseMessage(status)
			{
				Content = new StringContent(status == HttpStatusCode.OK && Body.StartsWith("{\"code\"", StringComparison.Ordinal) ? "{}" : Body, System.Text.Encoding.UTF8, MediaType),
			});
		}
	}
}
