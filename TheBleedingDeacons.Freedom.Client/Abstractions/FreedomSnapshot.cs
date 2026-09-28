// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Abstractions;

/// <summary>
/// Everything a store holds.
/// </summary>
/// <param name="Values">Every stored value, by key.</param>
/// <param name="Etag">The manifest ETag the values were complete at, or null when they are not known to be.</param>
/// <param name="VerifiedAt">When the server last confirmed these values, or null when it never has.</param>
public sealed record FreedomSnapshot(IReadOnlyDictionary<string, FreedomValue> Values, string? Etag, DateTimeOffset? VerifiedAt)
{
	/// <summary>A store with nothing in it.</summary>
	public static FreedomSnapshot Empty { get; } = new(new Dictionary<string, FreedomValue>(StringComparer.Ordinal), null, null);
}
