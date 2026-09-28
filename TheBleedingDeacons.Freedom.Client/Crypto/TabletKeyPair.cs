// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Security.Cryptography;

namespace TheBleedingDeacons.Freedom.Client.Crypto;

/// <summary>
/// The device's RSA-2048 keypair: the private half stays here, the public half
/// goes to the server at sign-in and every secret is sealed to it.
/// </summary>
/// <remarks>
/// Generated in managed code and kept by the <see cref="Abstractions.IFreedomCredentialStore"/>,
/// which on a device is platform secure storage. Not generated inside the
/// hardware keystore — the same compromise Link makes, and listed under "What
/// is not done".
/// </remarks>
/// <param name="PrivateKeyPem">The private key, PKCS#8 PEM.</param>
/// <param name="PublicKey">The public key, base64 SubjectPublicKeyInfo — what the server's <c>DevicePublicKey</c> accepts.</param>
public sealed record TabletKeyPair(string PrivateKeyPem, string PublicKey)
{
	/// <summary>The key size. The server refuses anything smaller.</summary>
	public const int Bits = 2048;

	/// <summary>A fresh keypair.</summary>
	public static TabletKeyPair Generate()
	{
		using var rsa = RSA.Create(Bits);

		return new TabletKeyPair(rsa.ExportPkcs8PrivateKeyPem(), Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));
	}

	/// <summary>Hides the private key from logs and debugger displays.</summary>
	public override string ToString() => $"TabletKeyPair {{ PublicKey = {PublicKey[..Math.Min(16, PublicKey.Length)]}… }}";
}
