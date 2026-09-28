// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Serialization;

/// <summary>The body of <c>POST config/values</c>.</summary>
/// <param name="Keys">The keys asked for.</param>
internal sealed record ValuesRequest(IReadOnlyList<string> Keys);
