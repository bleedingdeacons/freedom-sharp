// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Models;

/// <summary>
/// The answer to an exchange or a session hand-off: the tablet's token.
/// </summary>
public sealed class Enrolment
{
	/// <summary>Gets the bearer token. Returned once; the server keeps only its HMAC.</summary>
	public string Token { get; init; } = string.Empty;

	/// <summary>Gets the tablet row this token belongs to.</summary>
	public TabletInfo Tablet { get; init; } = new();

	/// <summary>Gets the application the tablet enrolled for.</summary>
	public ApplicationInfo Application { get; init; } = new();
}
