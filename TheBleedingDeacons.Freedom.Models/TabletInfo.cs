// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Models;

/// <summary>
/// What the server says about the tablet it enrolled.
/// </summary>
public sealed class TabletInfo
{
	/// <summary>Gets the tablet's id on the server.</summary>
	public long Id { get; init; }

	/// <summary>Gets the label the tablet enrolled with.</summary>
	public string Label { get; init; } = string.Empty;

	/// <summary>Gets when the tablet row was first created, as Unix seconds.</summary>
	public long CreatedAt { get; init; }

	/// <summary>Gets a value indicating whether this sign-in found an existing row for this device.</summary>
	public bool Reattached { get; init; }
}
