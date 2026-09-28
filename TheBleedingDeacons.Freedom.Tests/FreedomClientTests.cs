// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Net;
using TheBleedingDeacons.Freedom.Client;
using TheBleedingDeacons.Freedom.Tests.Support;

namespace TheBleedingDeacons.Freedom.Tests;

/// <summary>
/// Signing in and every start, against a server that keeps the plugin's rules.
/// The behaviour in the words the README uses is in the Specs project; this is
/// the edges, and what drives the coverage number.
/// </summary>
public sealed class FreedomClientTests : IDisposable
{
	private readonly Harness _h = new();

	public void Dispose() => _h.Dispose();

	// ── Signing in ────────────────────────────────────────────────────
	[Fact]
	public async Task ABrowserSignInStoresATokenAndAKeyAndThenSyncs()
	{
		_h.Server.Set("smtp.host", "mail.example.org");

		var result = await _h.SignInAsync();

		Assert.Equal(EnrolmentStatus.Enrolled, result.Status);
		Assert.True(result.Succeeded);
		var credentials = await _h.Credentials.LoadAsync(default);
		Assert.Equal(_h.Server.Token, credentials!.Token);
		Assert.Contains("PRIVATE KEY", credentials.PrivateKeyPem, StringComparison.Ordinal);
		Assert.Equal(SyncStatus.Updated, result.Sync!.Status);
		Assert.Equal("mail.example.org", _h.Client.Get("smtp.host"));
	}

	[Fact]
	public async Task SigningInAgainReattachesWithAFreshKey()
	{
		await _h.SignInAsync();
		var firstKey = _h.Server.PublicKey;

		var again = await _h.SignInAsync();

		Assert.Equal(EnrolmentStatus.Reattached, again.Status);
		Assert.NotEqual(firstKey, _h.Server.PublicKey, StringComparer.Ordinal);
	}

	[Fact]
	public async Task ClosingTheBrowserIsACancelNotAFailure()
	{
		_h.Browser.Closes = true;

		var result = await _h.SignInAsync();

		Assert.Equal(EnrolmentStatus.Cancelled, result.Status);
		Assert.False(await _h.Client.IsEnrolledAsync(TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task ARefusalInTheBrowserComesBackInWordsTheUserCanRead()
	{
		_h.Server.BrowserRefusal = "not_authorised";

		var result = await _h.SignInAsync();

		Assert.Equal(EnrolmentStatus.Refused, result.Status);
		Assert.Equal("not_authorised", result.Code);
		Assert.Contains("Google account", result.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ARefusalAtTheExchangeCarriesTheServersCode()
	{
		_h.Server.Refusal = "freedom_not_authorised";

		var result = await _h.SignInAsync();

		Assert.Equal(EnrolmentStatus.Refused, result.Status);
		Assert.Equal("freedom_not_authorised", result.Code);
	}

	[Fact]
	public async Task NoServerIsAFailureNotARefusal()
	{
		_h.Server.Offline = true;

		var result = await _h.SignInAsync();

		Assert.Equal(EnrolmentStatus.Failed, result.Status);
	}

	[Fact]
	public async Task AnExistingSessionSignsInWithoutABrowser()
	{
		var result = await _h.Client.EnrolAsync(FreedomProof.ExistingSession(FakeFreedomServer.LinkSession), TestContext.Current.CancellationToken);

		Assert.Equal(EnrolmentStatus.Enrolled, result.Status);
		Assert.Equal(0, _h.Browser.Opened);
		Assert.Equal(0, _h.Server.Count("auth/start"));
	}

	[Fact]
	public async Task AnApplicationThatDoesNotAcceptSessionsSaysSo()
	{
		_h.Server.AcceptSessions = false;

		var result = await _h.Client.EnrolAsync(FreedomProof.ExistingSession(FakeFreedomServer.LinkSession), TestContext.Current.CancellationToken);

		Assert.Equal(EnrolmentStatus.Refused, result.Status);
		Assert.Equal("freedom_sessions_not_accepted", result.Code);
	}

	[Fact]
	public async Task ABrowserSignInNeedsACallbackASignInAndADevice()
	{
		using var client = new FreedomClient(
			new FreedomOptions { BaseUrl = new Uri(FakeFreedomServer.BaseUrl), Application = "register" },
			_h.Store,
			_h.Credentials);

		var result = await client.EnrolAsync(cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(EnrolmentStatus.NotConfigured, result.Status);
	}

	[Fact]
	public async Task UnconfiguredOptionsAreReportedNotThrown()
	{
		using var client = new FreedomClient(
			new FreedomOptions { BaseUrl = new Uri("http://insecure.example.org"), Application = "register" },
			_h.Store,
			_h.Credentials);

		Assert.Equal(SyncStatus.NotConfigured, (await client.SyncAsync(TestContext.Current.CancellationToken)).Status);
		Assert.Equal(EnrolmentStatus.NotConfigured, (await client.EnrolAsync(cancellationToken: TestContext.Current.CancellationToken)).Status);
	}

	// ── Every start ───────────────────────────────────────────────────
	[Fact]
	public async Task BeforeSigningInThereIsNothingToSync()
	{
		Assert.Equal(SyncStatus.NotEnrolled, (await _h.StartAsync()).Status);
		Assert.Empty(_h.Server.Requests);
	}

	[Fact]
	public async Task NothingChangedCostsOneRequestAndA304()
	{
		_h.Server.Set("smtp.host", "mail");
		await _h.SignInAsync();
		var before = _h.Server.Requests.Count;

		var result = await _h.StartAsync();

		Assert.Equal(SyncStatus.UpToDate, result.Status);
		Assert.Equal(before + 1, _h.Server.Requests.Count);
		Assert.NotNull(result.VerifiedAt);
	}

	[Fact]
	public async Task OnlyStaleKeysAreFetched()
	{
		_h.Server.Set("smtp.host", "mail");
		_h.Server.Set("smtp.port", "587");
		await _h.SignInAsync();
		_h.Server.Set("smtp.port", "465");

		var result = await _h.StartAsync();

		Assert.Equal(SyncStatus.Updated, result.Status);
		Assert.Equal(["smtp.port"], result.Updated);
		Assert.Equal(["smtp.host", "smtp.port", "smtp.port"], _h.Server.RequestedKeys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
		Assert.Equal("465", _h.Client.Get("smtp.port"));
	}

	[Fact]
	public async Task AKeyTheManifestNoLongerListsIsRemoved()
	{
		_h.Server.Set("smtp.host", "mail");
		_h.Server.Set("old.setting", "x");
		await _h.SignInAsync();
		_h.Server.Remove("old.setting");

		var result = await _h.StartAsync();

		Assert.Equal(["old.setting"], result.Removed);
		Assert.Null(_h.Client.Get("old.setting"));
		Assert.Equal(["old.setting"], _h.Changes[^1].Removed);
	}

	[Fact]
	public async Task ASecretArrivesSealedAndIsStoredOpened()
	{
		_h.Server.Set("smtp.password", "correct horse", secret: true);

		await _h.SignInAsync();

		Assert.True(_h.Client.TryGet("smtp.password", out var value));
		Assert.Equal("correct horse", value.Value);
		Assert.True(value.IsSecret);
		Assert.DoesNotContain("correct horse", value.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task ASecretThatWillNotOpenKeepsTheOldValueAndTellsTheServer()
	{
		_h.Server.Set("smtp.password", "old", secret: true);
		await _h.SignInAsync();
		var current = (await _h.Store.LoadAsync(default)).Etag;
		_h.Server.SealTo = Client.Crypto.TabletKeyPair.Generate().PublicKey;
		_h.Server.Set("smtp.password", "new", secret: true);

		var result = await _h.StartAsync();

		Assert.Equal(SyncStatus.KeyFault, result.Status);
		Assert.Equal(["smtp.password"], result.Unopened);
		Assert.Equal("old", _h.Client.Get("smtp.password"));
		Assert.Equal(1, _h.Server.KeyFaults);

		// Not marked current at the new manifest, so the next start tries again.
		Assert.Equal(current, (await _h.Store.LoadAsync(default)).Etag);
	}

	[Fact]
	public async Task TheEtagIsSavedOnlyWhenEverythingApplied()
	{
		_h.Server.Set("a", "1");
		await _h.SignInAsync();
		var complete = (await _h.Store.LoadAsync(default)).Etag;
		_h.Server.Set("a", "2");
		_h.Server.ValuesStatus = HttpStatusCode.InternalServerError;

		await _h.StartAsync();

		// Nothing applied, so the store is still exactly what the old ETag
		// described — and the new manifest's ETag was not saved over it.
		Assert.NotNull(complete);
		Assert.Equal(complete, (await _h.Store.LoadAsync(default)).Etag);
		Assert.Equal("1", _h.Client.Get("a"));

		_h.Server.ValuesStatus = null;
		Assert.Equal(SyncStatus.Updated, (await _h.StartAsync()).Status);
		Assert.Equal("2", _h.Client.Get("a"));
	}

	[Fact]
	public async Task APartialSyncForgetsTheEtagSoTheNextStartChecksEverything()
	{
		_h.Server.Set("a", "1");
		_h.Server.Set("b", "1", secret: true);
		await _h.SignInAsync();
		_h.Server.Set("a", "2");
		_h.Server.SealTo = Client.Crypto.TabletKeyPair.Generate().PublicKey;
		_h.Server.Set("b", "2", secret: true);

		var result = await _h.StartAsync();

		Assert.Equal(["a"], result.Updated);
		Assert.Null((await _h.Store.LoadAsync(default)).Etag);
	}

	[Fact]
	public async Task VersionsAreComparedForDifferenceNotOrder()
	{
		// A tablet holding an override at v9 whose override is removed is
		// handed the default at v1 — lower, and still a change.
		_h.Server.Set("smtp.host", "default");
		await _h.SignInAsync();
		await _h.Store.ApplyAsync(new(new[] { new Client.Abstractions.FreedomValue("smtp.host", "override", 9, false) }, [], null, DateTimeOffset.UtcNow), default);

		var result = await _h.StartAsync();

		Assert.Equal(["smtp.host"], result.Updated);
		Assert.Equal("default", _h.Client.Get("smtp.host"));
	}

	// ── Refusals and failures ─────────────────────────────────────────
	[Fact]
	public async Task NoNetworkKeepsEverything()
	{
		_h.Server.Set("smtp.host", "mail");
		await _h.SignInAsync();
		_h.Server.Offline = true;

		var result = await _h.StartAsync();

		Assert.Equal(SyncStatus.Offline, result.Status);
		Assert.Equal("mail", _h.Client.Get("smtp.host"));
		Assert.NotNull(result.VerifiedAt);
	}

	[Theory]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.TooManyRequests)]
	[InlineData(HttpStatusCode.Forbidden)]
	public async Task AServerErrorIsNotARefusal(HttpStatusCode status)
	{
		_h.Server.Set("smtp.host", "mail");
		await _h.SignInAsync();
		_h.Server.ManifestStatus = status;

		var result = await _h.StartAsync();

		Assert.Equal(SyncStatus.ServerError, result.Status);
		Assert.Equal("mail", _h.Client.Get("smtp.host"));
		Assert.True(await _h.Client.IsEnrolledAsync(TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task ADisabledApplicationIsASuspensionThatKeepsEverything()
	{
		_h.Server.Set("smtp.host", "mail");
		await _h.SignInAsync();
		_h.Server.Disabled = true;

		var result = await _h.StartAsync();

		Assert.Equal(SyncStatus.Suspended, result.Status);
		Assert.Equal("mail", _h.Client.Get("smtp.host"));
	}

	[Fact]
	public async Task ARevokedTokenClearsEverything()
	{
		_h.Server.Set("smtp.password", "secret", secret: true);
		await _h.SignInAsync();
		_h.Server.Revoked = true;

		var result = await _h.StartAsync();

		Assert.Equal(SyncStatus.Revoked, result.Status);
		Assert.Empty(_h.Client.Current.Values);
		Assert.False(await _h.Client.IsEnrolledAsync(TestContext.Current.CancellationToken));
		Assert.Equal(["smtp.password"], _h.Changes[^1].Removed);
	}

	[Theory]
	[InlineData("freedom_not_authorised")]
	[InlineData("freedom_tablet_blocked")]
	public async Task ARefusalClearsEverything(string code)
	{
		_h.Server.Set("smtp.host", "mail");
		await _h.SignInAsync();
		_h.Server.Refusal = code;

		var result = await _h.StartAsync();

		Assert.Equal(SyncStatus.NotAuthorised, result.Status);
		Assert.Empty(_h.Client.Current.Values);
	}

	[Fact]
	public async Task ClearingOnRefusalCanBeTurnedOff()
	{
		using var h = new Harness(clearOnRefusal: false);
		h.Server.Set("smtp.host", "mail");
		await h.SignInAsync();
		h.Server.Revoked = true;

		var result = await h.StartAsync();

		Assert.Equal(SyncStatus.Revoked, result.Status);
		Assert.Equal("mail", h.Client.Get("smtp.host"));
	}

	[Fact]
	public async Task SigningOutRevokesAndClearsEvenWithoutAServer()
	{
		_h.Server.Set("smtp.host", "mail");
		await _h.SignInAsync();

		await _h.Client.SignOutAsync(TestContext.Current.CancellationToken);

		Assert.True(_h.Server.Revoked);
		Assert.Empty(_h.Client.Current.Values);
		Assert.False(await _h.Client.IsEnrolledAsync(TestContext.Current.CancellationToken));

		await _h.SignInAsync();
		_h.Server.Offline = true;
		await _h.Client.SignOutAsync(TestContext.Current.CancellationToken);
		Assert.False(await _h.Client.IsEnrolledAsync(TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task LoadReadsTheStoreWithoutTheServer()
	{
		_h.Server.Set("smtp.host", "mail");
		await _h.SignInAsync();
		_h.Server.Offline = true;

		using var fresh = _h.Build(_h.Options);
		await fresh.LoadAsync(TestContext.Current.CancellationToken);

		Assert.Equal("mail", fresh.Get("smtp.host"));
		Assert.Equal(1, _h.Server.Requests.Count(r => string.Equals(r.Path, "config/manifest", StringComparison.Ordinal)));
	}
}
