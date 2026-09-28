// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Abstractions;

/// <summary>
/// What a signed-in device keeps: its token and its private key.
/// </summary>
/// <param name="Token">The bearer token.</param>
/// <param name="TabletId">The server's id for this device.</param>
/// <param name="PrivateKeyPem">The private key every secret is sealed to, PKCS#8 PEM.</param>
public sealed record TabletCredentials(string Token, long TabletId, string PrivateKeyPem)
{
	/// <summary>Hides the token and the key from logs and debugger displays.</summary>
	public override string ToString() => $"TabletCredentials {{ TabletId = {TabletId} }}";
}
