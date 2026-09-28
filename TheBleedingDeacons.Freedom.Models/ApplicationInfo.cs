// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Models;

/// <summary>
/// The application a tablet enrolled for.
/// </summary>
public sealed class ApplicationInfo
{
	/// <summary>Gets the application's slug.</summary>
	public string Slug { get; init; } = string.Empty;

	/// <summary>Gets the application's display name.</summary>
	public string Name { get; init; } = string.Empty;
}
