// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using TheBleedingDeacons.Freedom.Client;
using TheBleedingDeacons.Freedom.Client.Abstractions;
using TheBleedingDeacons.Freedom.Client.Serialization;
using TheBleedingDeacons.Freedom.Models;

namespace TheBleedingDeacons.Freedom.Tests;

/// <summary>
/// The library's JSON is source-generated, so it works where reflection-based
/// serialization is off — trimmed and AOT builds, and file-based apps, where
/// the first request used to throw. The wire options resolve only through the
/// generated context, so a type missing from it fails here whatever the
/// process-wide switch says.
/// </summary>
public sealed class SerializationTests
{
	[Theory]
	[InlineData(typeof(SignInStart))]
	[InlineData(typeof(Enrolment))]
	[InlineData(typeof(Manifest))]
	[InlineData(typeof(ValuesResponse))]
	[InlineData(typeof(JsonElementBox))]
	[InlineData(typeof(ValuesRequest))]
	[InlineData(typeof(Dictionary<string, string>))]
	public void EveryWireTypeIsGenerated(Type type)
	{
		Assert.NotNull(FreedomApi.Json.TypeInfoResolver!.GetTypeInfo(type, FreedomApi.Json));
	}

	[Fact]
	public void AValuesRequestIsSnakeCase()
	{
		var json = System.Text.Json.JsonSerializer.Serialize(new ValuesRequest(["smtp.host"]), FreedomApi.Json);

		Assert.Equal("{\"keys\":[\"smtp.host\"]}", json);
	}

	[Fact]
	public void ASnapshotSurvivesARoundTrip()
	{
		var at = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
		var snapshot = new FreedomSnapshot(
			new Dictionary<string, FreedomValue>(StringComparer.Ordinal)
			{
				["smtp.password"] = new("smtp.password", "pässwörd ✓", 42, true),
			},
			"etag-1",
			at);

		var back = FreedomStateJson.DeserializeSnapshot(FreedomStateJson.Serialize(snapshot));

		Assert.Equal("pässwörd ✓", back.Values["smtp.password"].Value);
		Assert.True(back.Values["smtp.password"].IsSecret);
		Assert.Equal(42, back.Values["smtp.password"].Version);
		Assert.Equal("etag-1", back.Etag);
		Assert.Equal(at, back.VerifiedAt);
	}

	[Fact]
	public void CredentialsSurviveARoundTrip()
	{
		var back = FreedomStateJson.DeserializeCredentials(FreedomStateJson.Serialize(new TabletCredentials("frt_x", 12, "PEM")));

		Assert.Equal(new TabletCredentials("frt_x", 12, "PEM"), back);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("not json")]
	public void UnreadableStateReadsAsNothing(string? json)
	{
		Assert.Empty(FreedomStateJson.DeserializeSnapshot(json).Values);
		Assert.Null(FreedomStateJson.DeserializeCredentials(json));
	}
}
