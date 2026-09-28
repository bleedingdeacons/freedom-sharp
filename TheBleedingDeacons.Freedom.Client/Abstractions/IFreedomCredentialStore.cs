// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Abstractions;

/// <summary>
/// Where the device's token and private key are kept. Platform secure storage on a device.
/// </summary>
public interface IFreedomCredentialStore
{
	/// <summary>The stored credentials, or null when this device has not signed in.</summary>
	Task<TabletCredentials?> LoadAsync(CancellationToken cancellationToken);

	/// <summary>Replace the stored credentials.</summary>
	Task SaveAsync(TabletCredentials credentials, CancellationToken cancellationToken);

	/// <summary>Forget the stored credentials.</summary>
	Task ClearAsync(CancellationToken cancellationToken);
}
