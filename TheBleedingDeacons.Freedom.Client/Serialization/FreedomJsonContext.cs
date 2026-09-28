// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Text.Json.Serialization;
using TheBleedingDeacons.Freedom.Models;

namespace TheBleedingDeacons.Freedom.Client.Serialization;

/// <summary>
/// The wire's JSON, source-generated: snake_case, as WordPress writes it.
/// </summary>
/// <remarks>
/// Generated rather than reflected so the library works where reflection-based
/// serialization is switched off — trimmed and AOT builds, and .NET 10's
/// file-based apps, where the first call threw "Reflection-based serialization
/// has been disabled for this application". Every type the client sends or
/// reads is listed here; a type that is not fails loudly in the tests.
/// </remarks>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
	PropertyNameCaseInsensitive = true,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SignInStart))]
[JsonSerializable(typeof(Enrolment))]
[JsonSerializable(typeof(Manifest))]
[JsonSerializable(typeof(ValuesResponse))]
[JsonSerializable(typeof(JsonElementBox))]
[JsonSerializable(typeof(ValuesRequest))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class FreedomJsonContext : JsonSerializerContext
{
}
