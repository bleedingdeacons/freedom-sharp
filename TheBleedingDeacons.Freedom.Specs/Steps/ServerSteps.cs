// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Globalization;
using System.Net;
using Reqnroll;
using TheBleedingDeacons.Freedom.Client.Crypto;
using TheBleedingDeacons.Freedom.Specs.Support;

namespace TheBleedingDeacons.Freedom.Specs.Steps;

/// <summary>
/// What is true on the site: its values, and how it will answer.
/// </summary>
[Binding]
public sealed class ServerSteps(World world)
{
	[Given(@"^the application has ""(.+)"" set to ""(.*)""$")]
	[Given(@"^the value of ""(.+)"" changes to ""(.*)""$")]
	public void Plain(string key, string value) => world.Server.Set(key, value);

	[Given(@"^the application has a secret ""(.+)"" set to ""(.*)""$")]
	[Given(@"^the value of secret ""(.+)"" changes to ""(.*)""$")]
	public void Secret(string key, string value) => world.Server.Set(key, value, secret: true);

	[Given(@"^""(.+)"" is removed from the application$")]
	public void Removed(string key) => world.Server.Remove(key);

	[Given(@"^the server cannot be reached$")]
	public void Offline() => world.Server.Offline = true;

	[Given(@"^the server answers every manifest with (\d+)$")]
	public void ManifestStatus(int status) => world.Server.ManifestStatus = (HttpStatusCode)status;

	[Given(@"^the server answers every values request with (\d+)$")]
	public void ValuesStatus(int status) => world.Server.ValuesStatus = (HttpStatusCode)status;

	[Given(@"^the server answers values requests normally again$")]
	public void ValuesNormal() => world.Server.ValuesStatus = null;

	[Given(@"^the server will refuse the sign-in in the browser with ""(.+)""$")]
	public void BrowserRefusal(string error) => world.Server.BrowserRefusal = error;

	[Given(@"^the server will refuse the sign-in with ""(.+)""$")]
	public void ExchangeRefusal(string code) => world.Server.Refusal = code;

	[Given(@"^the application does not accept a Link session$")]
	public void NoSessions() => world.Server.AcceptSessions = false;

	[Given(@"^the device's token has been revoked$")]
	public void Revoked() => world.Server.Revoked = true;

	[Given(@"^the account may no longer use the application$")]
	public void NotAuthorised() => world.Server.Refusal = "freedom_not_authorised";

	[Given(@"^the device has been blocked$")]
	public void Blocked() => world.Server.Refusal = "freedom_tablet_blocked";

	[Given(@"^the application has been disabled$")]
	public void Disabled() => world.Server.Disabled = true;

	[Given(@"^secrets are now sealed to a key this device does not hold$")]
	public void WrongKey() => world.Server.SealTo = TabletKeyPair.Generate().PublicKey;

	[Given(@"^secrets are sealed to the device's own key again$")]
	public void RightKey() => world.Server.SealTo = null;

	[Given(@"^the user will close the browser$")]
	public void Closes() => world.Device.Browser.Closes = true;

	[Given(@"^the device holds ""(.+)"" as ""(.*)"" at version (\d+)$")]
	public async Task Holds(string key, string value, int version) =>
		await world.Device.Store.ApplyAsync(
			new(
				[new Client.Abstractions.FreedomValue(key, value, long.Parse(version.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), false)],
				[],
				null,
				DateTimeOffset.UtcNow),
			default);
}
