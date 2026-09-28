// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// How a sign-in ended.
/// </summary>
public enum EnrolmentStatus
{
	/// <summary>A new device row was created.</summary>
	Enrolled,

	/// <summary>This device's existing row was found and given a new token and key.</summary>
	Reattached,

	/// <summary>The user closed the browser. Not a failure, and nothing to say about it.</summary>
	Cancelled,

	/// <summary>The server refused: see <see cref="EnrolmentResult.Code"/> and <see cref="EnrolmentResult.Message"/>.</summary>
	Refused,

	/// <summary>The server could not be reached or answered with an error.</summary>
	Failed,

	/// <summary>The options are incomplete, or no browser sign-in or device identity was supplied.</summary>
	NotConfigured,
}
