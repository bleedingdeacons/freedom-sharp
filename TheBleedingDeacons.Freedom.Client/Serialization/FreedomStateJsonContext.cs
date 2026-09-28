// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Text.Json.Serialization;
using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace TheBleedingDeacons.Freedom.Client.Serialization;

/// <summary>The persisted state's JSON, source-generated.</summary>
[JsonSerializable(typeof(StoredSnapshot))]
[JsonSerializable(typeof(TabletCredentials))]
internal sealed partial class FreedomStateJsonContext : JsonSerializerContext
{
}
