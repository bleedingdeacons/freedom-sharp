// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace TheBleedingDeacons.Freedom.Client.Serialization;

/// <summary>A snapshot as it is written down.</summary>
/// <param name="Values">Every stored value.</param>
/// <param name="Etag">The manifest ETag the values were complete at.</param>
/// <param name="VerifiedAt">When the server last confirmed them.</param>
internal sealed record StoredSnapshot(List<FreedomValue> Values, string? Etag, DateTimeOffset? VerifiedAt);
