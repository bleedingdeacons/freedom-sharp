// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Security.Cryptography;
using TheBleedingDeacons.Freedom.Client.Crypto;
using TheBleedingDeacons.Freedom.Tests.Support;

namespace TheBleedingDeacons.Freedom.Tests;

/// <summary>
/// Opening what the plugin seals. <see cref="Sealing"/> builds envelopes the
/// way Fellowship's MessageSealer does; the plugin's ConfigApiTest opens them
/// the way <see cref="EnvelopeOpener"/> does. Each side's test does the other
/// side's job.
/// </summary>
public sealed class EnvelopeOpenerTests
{
	private static readonly TabletKeyPair Keys = TabletKeyPair.Generate();

	[Fact]
	public void ItOpensWhatThePluginSeals()
	{
		var (k, p) = Sealing.Seal("smtp.password", 42, "correct horse battery staple", Keys.PublicKey);

		Assert.Equal("correct horse battery staple", EnvelopeOpener.Open("smtp.password", 42, k, p, Keys.PrivateKeyPem));
	}

	[Fact]
	public void ItOpensAnEmptyValueAndUnicode()
	{
		var (k, p) = Sealing.Seal("a", 1, string.Empty, Keys.PublicKey);
		var (k2, p2) = Sealing.Seal("b", 2, "pässwörd ✓", Keys.PublicKey);

		Assert.Equal(string.Empty, EnvelopeOpener.Open("a", 1, k, p, Keys.PrivateKeyPem));
		Assert.Equal("pässwörd ✓", EnvelopeOpener.Open("b", 2, k2, p2, Keys.PrivateKeyPem));
	}

	[Fact]
	public void AnotherDevicesKeyOpensNothing()
	{
		var (k, p) = Sealing.Seal("smtp.password", 42, "secret", Keys.PublicKey);

		Assert.Null(EnvelopeOpener.Open("smtp.password", 42, k, p, TabletKeyPair.Generate().PrivateKeyPem));
	}

	[Fact]
	public void OaepMustBeSha1()
	{
		// The line most likely to be "improved" into a bug. PHP offers only
		// SHA-1; an envelope wrapped with SHA-256 is one the plugin never
		// sends, and must not open — nor must switching the opener to SHA-256
		// quietly pass this suite.
		var (k, p) = Sealing.Seal("smtp.password", 42, "secret", Keys.PublicKey, RSAEncryptionPadding.OaepSHA256);

		Assert.Null(EnvelopeOpener.Open("smtp.password", 42, k, p, Keys.PrivateKeyPem));
	}

	[Fact]
	public void ASealedValueCannotBeMovedToAnotherKey()
	{
		var (k, p) = Sealing.Seal("smtp.password", 42, "secret", Keys.PublicKey);

		Assert.Null(EnvelopeOpener.Open("unity.api_key", 42, k, p, Keys.PrivateKeyPem));
		Assert.Null(EnvelopeOpener.Open("smtp.password", 43, k, p, Keys.PrivateKeyPem));
	}

	[Fact]
	public void ATamperedPayloadDoesNotOpen()
	{
		var (k, p) = Sealing.Seal("smtp.password", 42, "secret", Keys.PublicKey);
		var bytes = Convert.FromBase64String(p);
		bytes[^1] ^= 0x01;

		Assert.Null(EnvelopeOpener.Open("smtp.password", 42, k, Convert.ToBase64String(bytes), Keys.PrivateKeyPem));
	}

	[Theory]
	[InlineData("", "cA==")]
	[InlineData("not base64!", "cA==")]
	[InlineData("aw==", "")]
	[InlineData("aw==", "c2hvcnQ=")]
	public void MalformedFieldsOpenNothing(string k, string p)
	{
		Assert.Null(EnvelopeOpener.Open("x", 1, k, p, Keys.PrivateKeyPem));
	}

	[Fact]
	public void AnUnreadablePrivateKeyOpensNothing()
	{
		var (k, p) = Sealing.Seal("x", 1, "v", Keys.PublicKey);

		Assert.Null(EnvelopeOpener.Open("x", 1, k, p, "-----BEGIN PRIVATE KEY-----\nnope\n-----END PRIVATE KEY-----"));
		Assert.Null(EnvelopeOpener.Open("x", 1, k, p, string.Empty));
	}

	[Fact]
	public void AKeyPairIsWhatThePluginAccepts()
	{
		using var rsa = RSA.Create();
		rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(Keys.PublicKey), out _);

		Assert.Equal(2048, rsa.KeySize);
		Assert.StartsWith("-----BEGIN PRIVATE KEY-----", Keys.PrivateKeyPem, StringComparison.Ordinal);
		Assert.DoesNotContain("PRIVATE", Keys.ToString(), StringComparison.Ordinal);
	}
}
