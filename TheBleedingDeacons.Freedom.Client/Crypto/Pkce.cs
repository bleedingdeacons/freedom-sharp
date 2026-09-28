// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Security.Cryptography;
using System.Text;

namespace TheBleedingDeacons.Freedom.Client.Crypto;

/// <summary>
/// RFC 7636 PKCE, S256, between the app and the Freedom plugin.
/// </summary>
/// <remarks>
/// The one-time code comes back through a custom URI scheme, and any other app
/// on the device that registers the same scheme can receive it. The verifier
/// never leaves this process — only its SHA-256 goes to the server — and the
/// exchange needs it, so a code caught by another app is worthless.
/// </remarks>
public static class Pkce
{
	/// <summary>A fresh verifier: 32 random bytes, base64url, 43 characters.</summary>
	public static string NewVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

	/// <summary>The S256 challenge for a verifier.</summary>
	public static string ChallengeFor(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

	private static string Base64Url(byte[] bytes) =>
		Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
