// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Text.Json;
using TheBleedingDeacons.Freedom.Client;
using TheBleedingDeacons.Freedom.Client.Crypto;
using TheBleedingDeacons.Freedom.Models;

namespace TheBleedingDeacons.Freedom.Tests;

/// <summary>
/// The wire shape, read from the fixtures the plugin commits too, and PKCE,
/// checked against the RFC so both ends agree on the encoding.
/// </summary>
public sealed class WireTests
{
	[Fact]
	public void TheManifestFixtureReads()
	{
		var manifest = Read<Manifest>("manifest.json");

		Assert.Equal("register", manifest.Application);
		Assert.Equal(12, manifest.Tablet);
		Assert.Equal(1_790_000_000, manifest.CheckedAt);
		Assert.Collection(
			manifest.Keys,
			e => Assert.Equal(("smtp.host", 41L, false), (e.Key, e.Version, e.Secret)),
			e => Assert.Equal(("smtp.password", 42L, true), (e.Key, e.Version, e.Secret)));
	}

	[Fact]
	public void TheValuesFixtureReads()
	{
		var values = Read<ValuesResponse>("values.json");

		Assert.Equal("mail.example.org", values.Values[0].Value);
		Assert.Null(values.Values[0].K);
		Assert.True(values.Values[1].Secret);
		Assert.Null(values.Values[1].Value);
		Assert.NotNull(values.Values[1].K);
		Assert.NotNull(values.Values[1].P);
		Assert.Equal(["nothing.here"], values.Missing);
		Assert.Empty(values.Unreadable);
	}

	[Fact]
	public void TheEnrolmentFixtureReads()
	{
		var enrolment = Read<Enrolment>("enrolment.json");

		Assert.StartsWith("frt_", enrolment.Token, StringComparison.Ordinal);
		Assert.Equal(12, enrolment.Tablet.Id);
		Assert.Equal(1_790_000_000, enrolment.Tablet.CreatedAt);
		Assert.False(enrolment.Tablet.Reattached);
		Assert.Equal("register", enrolment.Application.Slug);
	}

	[Fact]
	public void PkceMatchesTheRfcExample()
	{
		// RFC 7636 Appendix B. The plugin's RulesTest checks the same pair.
		Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", Pkce.ChallengeFor("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
	}

	[Fact]
	public void AVerifierIsWhatThePluginAccepts()
	{
		var verifier = Pkce.NewVerifier();

		Assert.Equal(43, verifier.Length);
		Assert.Matches("^[A-Za-z0-9_-]{43}$", verifier);
		Assert.NotEqual(verifier, Pkce.NewVerifier(), StringComparer.Ordinal);
	}

	[Theory]
	[InlineData("https://example.org", false, true)]
	[InlineData("http://example.org", false, false)]
	[InlineData("http://stalwart-dev.local", true, true)]
	[InlineData("ftp://example.org", true, false)]
	public void OnlyHttpsIsSomewhereFreedomWillTalkTo(string url, bool allowInsecure, bool configured)
	{
		var options = new FreedomOptions { BaseUrl = new Uri(url), Application = "register", AllowInsecureBaseUrl = allowInsecure };

		Assert.Equal(configured, options.IsConfigured);
	}

	private static T Read<T>(string file) =>
		JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", file)), FreedomApi.Json)!;
}
