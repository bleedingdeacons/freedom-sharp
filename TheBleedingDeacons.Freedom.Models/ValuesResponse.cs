// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Models;

/// <summary>
/// The answer to <c>POST /freedom/v1/config/values</c>.
/// </summary>
public sealed class ValuesResponse
{
	/// <summary>Gets the values that could be served.</summary>
	public IReadOnlyList<ValueEntry> Values { get; init; } = [];

	/// <summary>Gets keys asked for that are not set.</summary>
	public IReadOnlyList<string> Missing { get; init; } = [];

	/// <summary>Gets keys that are set but could not be served: a value that no longer decrypts, or a key the server could not seal to.</summary>
	public IReadOnlyList<string> Unreadable { get; init; } = [];
}
