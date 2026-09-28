// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Models;

/// <summary>
/// One served value: plain in <see cref="Value"/>, or sealed in <see cref="K"/> and <see cref="P"/>.
/// </summary>
public sealed class ValueEntry
{
	/// <summary>Gets the key.</summary>
	public string Key { get; init; } = string.Empty;

	/// <summary>Gets the version.</summary>
	public long Version { get; init; }

	/// <summary>Gets a value indicating whether this value is sealed.</summary>
	public bool Secret { get; init; }

	/// <summary>Gets the plain value. Null for a secret.</summary>
	public string? Value { get; init; }

	/// <summary>Gets the content key, RSA-OAEP-SHA1 to this tablet, base64. Secrets only.</summary>
	public string? K { get; init; }

	/// <summary>Gets the sealed payload — nonce(12), tag(16), ciphertext — base64. Secrets only.</summary>
	public string? P { get; init; }
}
