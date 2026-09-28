// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace TheBleedingDeacons.Freedom.Client.Crypto;

/// <summary>
/// Opens a secret value the server sealed to this device.
/// </summary>
/// <remarks>
/// <para><b>Fellowship's envelope, unchanged</b> — the one Link opens. The
/// server makes a fresh 32-byte content key per value, seals gzipped JSON under
/// it with AES-256-GCM, and wraps the content key to this device's public key
/// with RSA-OAEP. Two fields arrive: <c>k</c>, the wrapped key, and <c>p</c>,
/// the sealed payload laid out as nonce(12) | tag(16) | ciphertext, both base64.</para>
/// <para><b>OAEP with SHA-1, deliberately.</b> PHP's
/// <c>OPENSSL_PKCS1_OAEP_PADDING</c> offers nothing else. OAEP relies on
/// preimage resistance, which SHA-1 still has. Changing this to
/// <see cref="RSAEncryptionPadding.OaepSHA256"/> without changing the server
/// produces a value that arrives and silently will not open. It is the line in
/// this library most likely to be "improved" into a bug; the tests fail if it is.</para>
/// <para><b>The key and version travel inside the seal</b>, and
/// <see cref="Open"/> refuses an envelope whose inner key or version is not the
/// one it arrived under, so a sealed value cannot be moved into another key's
/// place in a response.</para>
/// </remarks>
public static class EnvelopeOpener
{
	private const int NonceBytes = 12;
	private const int TagBytes = 16;
	private const int ContentKeyBytes = 32;

	/// <summary>
	/// The plaintext of a sealed value, or null when it cannot be opened — a key
	/// this device no longer holds, a truncated or tampered payload, or an inner
	/// key or version that is not <paramref name="key"/> and <paramref name="version"/>.
	/// </summary>
	public static string? Open(string key, long version, string wrappedKey, string sealedPayload, string privateKeyPem)
	{
		using var opened = OpenRaw(wrappedKey, sealedPayload, privateKeyPem);
		if (opened is null)
		{
			return null;
		}

		if (!opened.RootElement.TryGetProperty("key", out var innerKey)
			|| innerKey.ValueKind != JsonValueKind.String
			|| !string.Equals(innerKey.GetString(), key, StringComparison.Ordinal))
		{
			return null;
		}

		if (!opened.RootElement.TryGetProperty("version", out var innerVersion)
			|| innerVersion.ValueKind != JsonValueKind.Number
			|| !innerVersion.TryGetInt64(out var v)
			|| v != version)
		{
			return null;
		}

		if (!opened.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String)
		{
			return null;
		}

		return value.GetString();
	}

	private static JsonDocument? OpenRaw(string wrappedKey, string sealedPayload, string privateKeyPem)
	{
		if (string.IsNullOrEmpty(wrappedKey) || string.IsNullOrEmpty(sealedPayload) || string.IsNullOrEmpty(privateKeyPem))
		{
			return null;
		}

		byte[] wrapped;
		byte[] packed;
		try
		{
			wrapped = Convert.FromBase64String(wrappedKey);
			packed = Convert.FromBase64String(sealedPayload);
		}
		catch (FormatException)
		{
			return null;
		}

		if (packed.Length <= NonceBytes + TagBytes)
		{
			return null;
		}

		byte[] contentKey;
		try
		{
			using var rsa = RSA.Create();
			rsa.ImportFromPem(privateKeyPem);
			contentKey = rsa.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA1);
		}
		catch (Exception e) when (e is CryptographicException or ArgumentException)
		{
			return null;
		}

		if (contentKey.Length != ContentKeyBytes)
		{
			return null;
		}

		var compressed = new byte[packed.Length - NonceBytes - TagBytes];
		try
		{
			using var gcm = new AesGcm(contentKey, TagBytes);
			gcm.Decrypt(packed.AsSpan(0, NonceBytes), packed.AsSpan(NonceBytes + TagBytes), packed.AsSpan(NonceBytes, TagBytes), compressed);
		}
		catch (CryptographicException)
		{
			// The tag did not verify: a wrong key, or a payload altered on the way.
			return null;
		}
		finally
		{
			CryptographicOperations.ZeroMemory(contentKey);
		}

		try
		{
			using var input = new MemoryStream(compressed);
			using var gzip = new GZipStream(input, CompressionMode.Decompress);

			return JsonDocument.Parse(gzip);
		}
		catch (Exception e) when (e is InvalidDataException or JsonException)
		{
			return null;
		}
	}
}
