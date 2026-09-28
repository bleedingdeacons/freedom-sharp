// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Abstractions;

/// <summary>
/// What one sync changed, to be applied in one step.
/// </summary>
/// <param name="Upserts">Values fetched this time.</param>
/// <param name="Removals">Keys the manifest no longer lists.</param>
/// <param name="Etag">The manifest's ETag when every stale key was applied; null when something was not, so the next start checks again.</param>
/// <param name="VerifiedAt">When the server answered.</param>
public sealed record FreedomChangeSet(IReadOnlyList<FreedomValue> Upserts, IReadOnlyList<string> Removals, string? Etag, DateTimeOffset VerifiedAt);
