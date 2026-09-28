// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using Reqnroll;
using Shouldly;
using TheBleedingDeacons.Freedom.Client;
using TheBleedingDeacons.Freedom.Specs.Support;

namespace TheBleedingDeacons.Freedom.Specs.Steps;

/// <summary>
/// What came of it: what the device holds, what it was told, and what the
/// server saw.
/// </summary>
[Binding]
public sealed class OutcomeSteps(World world)
{
	// ── Signing in ────────────────────────────────────────────────────
	[Then(@"^the sign-in succeeded$")]
	public void Succeeded() => world.Enrolment.ShouldNotBeNull().Succeeded.ShouldBeTrue(world.Enrolment?.Message);

	[Then(@"^the sign-in was cancelled$")]
	public void Cancelled() => world.Enrolment.ShouldNotBeNull().Status.ShouldBe(EnrolmentStatus.Cancelled);

	[Then(@"^the sign-in failed$")]
	public void Failed() => world.Enrolment.ShouldNotBeNull().Status.ShouldBe(EnrolmentStatus.Failed);

	[Then(@"^the sign-in was not configured$")]
	public void NotConfigured() => world.Enrolment.ShouldNotBeNull().Status.ShouldBe(EnrolmentStatus.NotConfigured);

	[Then(@"^the sign-in was refused with ""(.+)""$")]
	public void Refused(string code)
	{
		var enrolment = world.Enrolment.ShouldNotBeNull();
		enrolment.Status.ShouldBe(EnrolmentStatus.Refused);
		enrolment.Code.ShouldBe(code);
	}

	[Then(@"^the message says ""(.+)""$")]
	public void MessageSays(string text) => world.Enrolment.ShouldNotBeNull().Message.ShouldContain(text);

	[Then(@"^the device was re-attached$")]
	public void Reattached() => world.Enrolment.ShouldNotBeNull().Status.ShouldBe(EnrolmentStatus.Reattached);

	[Then(@"^the device sent a new public key$")]
	public void NewKey() =>
		world.Server.Requests.Count(r => string.Equals(r.Path, "auth/exchange", StringComparison.Ordinal)).ShouldBe(2);

	[Then(@"^the browser was never opened$")]
	public void NoBrowser()
	{
		world.Device.Browser.Opened.ShouldBe(0);
		world.Server.Count("auth/start").ShouldBe(0);
	}

	[Then(@"^the device is (signed in|not signed in)$")]
	public async Task SignedIn(string state) =>
		(await world.Client.IsEnrolledAsync(TestContext.Current.CancellationToken)).ShouldBe(string.Equals(state, "signed in", StringComparison.Ordinal));

	// ── Starting ──────────────────────────────────────────────────────
	[Then(@"^the start reports (\w+)$")]
	public void Reports(SyncStatus status) => world.LastSync.ShouldNotBeNull().Status.ShouldBe(status);

	[Then(@"^only ""(.+)"" was fetched$")]
	public void OnlyFetched(string key) => world.LastSync.ShouldNotBeNull().Updated.ShouldBe([key]);

	[Then(@"^the device knows how old its configuration is$")]
	public void KnowsAge() => world.LastSync.ShouldNotBeNull().VerifiedAt.ShouldNotBeNull();

	[Then(@"^the configuration is not marked current at the new manifest$")]
	public async Task NotCurrent()
	{
		var stored = await world.Device.Store.LoadAsync(default);
		var manifest = await new FreedomApi(new Uri(Tests.Support.FakeFreedomServer.BaseUrl), new HttpClient(world.Server, disposeHandler: false))
			.GetManifestAsync(world.Server.Token!, null, TestContext.Current.CancellationToken);

		stored.Etag.ShouldNotBe(manifest.Data.ShouldNotBeNull().Etag, StringComparer.Ordinal);
	}

	[Then(@"^the app was told ""(.+)"" changed$")]
	public void ToldChanged(string key) => world.Device.Changes.ShouldContain(c => c.Updated.Contains(key));

	[Then(@"^the app was told ""(.+)"" was removed$")]
	public void ToldRemoved(string key) => world.Device.Changes.ShouldContain(c => c.Removed.Contains(key));

	// ── What the device holds ─────────────────────────────────────────
	[Then(@"^the device holds ""(.+)"" as ""(.*)""$")]
	public void Holds(string key, string value) => world.Client.Get(key).ShouldBe(value);

	[Then(@"^the device does not hold ""(.+)""$")]
	public void DoesNotHold(string key) => world.Client.Get(key).ShouldBeNull();

	[Then(@"^the device holds nothing$")]
	public async Task HoldsNothing()
	{
		world.Client.Current.Values.ShouldBeEmpty();
		(await world.Device.Store.LoadAsync(default)).Values.ShouldBeEmpty();
	}

	[Then(@"^""(.+)"" is held as a secret$")]
	public void HeldAsSecret(string key)
	{
		world.Client.TryGet(key, out var value).ShouldBeTrue();
		value.IsSecret.ShouldBeTrue();
	}

	// ── What the server saw ───────────────────────────────────────────
	[Then(@"^the server was told the device cannot open a secret$")]
	public void KeyFaultReported() => world.Server.KeyFaults.ShouldBe(1);

	[Then(@"^the server revoked the token$")]
	public void ServerRevoked() => world.Server.Revoked.ShouldBeTrue();

	[Then(@"^the requests went to ""(.+)""$")]
	public void WentTo(string prefix) => (world.Server.SitePath + "/wp-json/freedom/v1/").ShouldBe(prefix);
}
