// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// What a sync found. Only <see cref="Revoked"/> and <see cref="NotAuthorised"/> clear anything.
/// </summary>
public enum SyncStatus
{
	/// <summary>This device has not signed in. Call <see cref="FreedomClient.EnrolAsync"/>.</summary>
	NotEnrolled,

	/// <summary>The server confirmed the stored values are current.</summary>
	UpToDate,

	/// <summary>Values were fetched or removed.</summary>
	Updated,

	/// <summary>The server could not be reached. Stored values are kept.</summary>
	Offline,

	/// <summary>The server answered with an error that is not a refusal. Stored values are kept.</summary>
	ServerError,

	/// <summary>The application is disabled. Stored values are kept.</summary>
	Suspended,

	/// <summary>The token is no longer valid. Sign in again; stored values are cleared unless the options say otherwise.</summary>
	Revoked,

	/// <summary>This account may no longer use the application, or the device is blocked. Stored values are cleared unless the options say otherwise.</summary>
	NotAuthorised,

	/// <summary>A secret arrived that this device cannot open. The old value is kept and the server told; sign in again to replace the key.</summary>
	KeyFault,

	/// <summary>The options are incomplete: no address, no application, or plain http without permission.</summary>
	NotConfigured,
}
