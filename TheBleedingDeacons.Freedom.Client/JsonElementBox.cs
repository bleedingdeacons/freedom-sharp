// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// An answer whose content does not matter — a sign-out, a key-fault report —
/// kept as whatever JSON came back.
/// </summary>
public sealed class JsonElementBox
{
	/// <summary>Gets the fields the server sent.</summary>
	[JsonExtensionData]
	public Dictionary<string, JsonElement> Fields { get; init; } = [];
}
