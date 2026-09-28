// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using Reqnroll;
using Shouldly;
using TheBleedingDeacons.Freedom.Client;
using TheBleedingDeacons.Freedom.Specs.Support;
using TheBleedingDeacons.Freedom.Tests.Support;

namespace TheBleedingDeacons.Freedom.Specs.Steps;

/// <summary>
/// What the device does: sign in, start, sign out — and how it is built.
/// </summary>
[Binding]
public sealed class DeviceSteps(World world)
{
	private int _requestsBefore;

	[Given(@"^the device does not clear on refusal$")]
	public void KeepOnRefusal() => world.KeepOnRefusal();

	[Given(@"^the device is built to use ""(.+)""$")]
	public void BuiltFor(string url) => Build(world.Device.Options with { BaseUrl = new Uri(url) });

	[Given(@"^the device is built to use ""(.+)"" allowing plain http$")]
	public void BuiltForInsecure(string url) => Build(world.Device.Options with { BaseUrl = new Uri(url), AllowInsecureBaseUrl = true });

	[Given(@"^the device is built with no callback URI$")]
	public void NoCallback() => Build(world.Device.Options with { CallbackUri = null });

	[Given(@"^the device has signed in$")]
	public async Task HasSignedIn()
	{
		(await world.Device.SignInAsync()).Succeeded.ShouldBeTrue();
	}

	[Given(@"^the device has signed in with Link's session$")]
	public async Task HasHandedOver()
	{
		(await world.Client.EnrolAsync(FreedomProof.ExistingSession(FakeFreedomServer.LinkSession), TestContext.Current.CancellationToken)).Succeeded.ShouldBeTrue();
	}

	[Given(@"^the app has started$")]
	public async Task HasStarted() => world.LastSync = await world.Device.StartAsync();

	[When(@"^the device signs in(?: again)?$")]
	public async Task SignsIn() => world.Enrolment = await world.Device.SignInAsync();

	[When(@"^the device signs in with Link's session$")]
	public async Task HandsOver() =>
		world.Enrolment = await world.Client.EnrolAsync(FreedomProof.ExistingSession(FakeFreedomServer.LinkSession), TestContext.Current.CancellationToken);

	[When(@"^the app starts$")]
	public async Task Starts()
	{
		_requestsBefore = world.Server.Requests.Count;
		world.LastSync = await world.Device.StartAsync();
	}

	[When(@"^the device signs out$")]
	public async Task SignsOut() => await world.Client.SignOutAsync(TestContext.Current.CancellationToken);

	[Then(@"^the start cost one request$")]
	public void OneRequest() => (world.Server.Requests.Count - _requestsBefore).ShouldBe(1);

	[Then(@"^the device is configured$")]
	public void Configured() => world.BuiltWith.ShouldNotBeNull().IsConfigured.ShouldBeTrue();

	private void Build(FreedomOptions options)
	{
		world.BuiltWith = options;
		world.Device.Build(options);
	}
}
