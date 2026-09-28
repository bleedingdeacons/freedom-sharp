// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TheBleedingDeacons.Freedom.Tests.Support;

/// <summary>
/// Fellowship's <c>MessageSealer::seal()</c>, transcribed into C# — the way the
/// Freedom plugin seals a secret value. Seals by construction; never calls the
/// code under test.
/// </summary>
/// <remarks>
/// Each side's test does the other side's job: the plugin's ConfigApiTest opens
/// an envelope the way <c>EnvelopeOpener</c> does, and this builds one the way
/// the plugin does. Drift on either turns the opposite one red. If one changes,
/// both change.
/// <list type="bullet">
/// <item>JSON of <c>{key, version, value}</c>, <c>wp_json_encode</c> style.</item>
/// <item>gzip (<c>gzencode</c>).</item>
/// <item>AES-256-GCM under a fresh 32-byte key, 12-byte nonce, 16-byte tag.</item>
/// <item><c>p</c> = base64(nonce | tag | ciphertext).</item>
/// <item><c>k</c> = base64(RSA-OAEP-SHA1(content key)) — <c>OPENSSL_PKCS1_OAEP_PADDING</c>.</item>
/// </list>
/// Linked into the Specs project too; nothing can reference a test project.
/// </remarks>
public static class Sealing
{
	public static (string K, string P) Seal(string key, long version, string value, string publicKeySpki, RSAEncryptionPadding? padding = null)
	{
		var json = JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal) { ["key"] = key, ["version"] = version, ["value"] = value });

		using var compressedStream = new MemoryStream();
		using (var gzip = new GZipStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
		{
			gzip.Write(Encoding.UTF8.GetBytes(json));
		}

		var compressed = compressedStream.ToArray();
		var contentKey = RandomNumberGenerator.GetBytes(32);
		var nonce = RandomNumberGenerator.GetBytes(12);
		var tag = new byte[16];
		var ciphertext = new byte[compressed.Length];

		using (var gcm = new AesGcm(contentKey, 16))
		{
			gcm.Encrypt(nonce, compressed, ciphertext, tag);
		}

		using var rsa = RSA.Create();
		rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeySpki), out _);
		var wrapped = rsa.Encrypt(contentKey, padding ?? RSAEncryptionPadding.OaepSHA1);

		return (Convert.ToBase64String(wrapped), Convert.ToBase64String([.. nonce, .. tag, .. ciphertext]));
	}
}
