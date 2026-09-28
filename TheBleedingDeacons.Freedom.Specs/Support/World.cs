// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using TheBleedingDeacons.Freedom.Client;
using TheBleedingDeacons.Freedom.Tests.Support;

namespace TheBleedingDeacons.Freedom.Specs.Support;

/// <summary>
/// One device and the server it talks to, for the length of a scenario.
///
/// <para>Reqnroll gives each scenario its own instance, so every step class
/// that takes this in its constructor sees the same device. The client is the
/// real one; the server is <see cref="FakeFreedomServer"/>, which keeps the
/// plugin's rules — versions that never repeat, a complete manifest, secrets
/// sealed to the enrolled key, the plugin's refusal codes.</para>
/// </summary>
public sealed class World : IDisposable
{
	public World()
	{
		Device = new Harness();
	}

	public Harness Device { get; private set; }

	public FakeFreedomServer Server => Device.Server;

	public FreedomClient Client => Device.Client;

	public EnrolmentResult? Enrolment { get; set; }

	public SyncResult? LastSync { get; set; }

	/// <summary>The options the device was last built with.</summary>
	public FreedomOptions? BuiltWith { get; set; }

	/// <summary>Start again with a device that does not clear on refusal.</summary>
	public void KeepOnRefusal()
	{
		Device.Dispose();
		Device = new Harness(clearOnRefusal: false);
	}

	public void Dispose() => Device.Dispose();
}
