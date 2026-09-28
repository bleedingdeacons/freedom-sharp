// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Abstractions;

/// <summary>
/// Who this device says it is. Reported, not proved: the Google account is the proof.
/// </summary>
public interface IDeviceIdentity
{
	/// <summary>This device's identity.</summary>
	Task<DeviceIdentity> GetAsync(CancellationToken cancellationToken);
}
