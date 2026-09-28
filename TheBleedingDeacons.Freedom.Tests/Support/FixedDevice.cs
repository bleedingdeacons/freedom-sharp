// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace TheBleedingDeacons.Freedom.Tests.Support;

/// <summary>A device that says the same thing every time.</summary>
public sealed class FixedDevice : IDeviceIdentity
{
	public Task<DeviceIdentity> GetAsync(CancellationToken cancellationToken) =>
		Task.FromResult(new DeviceIdentity("9774d56d682e549c", "android", "Hall tablet", "SM-X200", "1.3.0"));
}
