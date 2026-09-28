// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Models;

/// <summary>
/// One key in the manifest.
/// </summary>
public sealed class ManifestEntry
{
	/// <summary>Gets the key.</summary>
	public string Key { get; init; } = string.Empty;

	/// <summary>Gets the version. Never reused within an application; compare for difference, not order.</summary>
	public long Version { get; init; }

	/// <summary>Gets a value indicating whether the value arrives sealed to this tablet's key.</summary>
	public bool Secret { get; init; }
}
