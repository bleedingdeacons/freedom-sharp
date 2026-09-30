// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Security.Cryptography;

namespace TheBleedingDeacons.Freedom.Client.Maui;

/// <summary>
/// What SecureStorage throws when what it holds cannot be read.
/// </summary>
/// <remarks>
/// A keystore that invalidated its key, a push that woke the device before
/// its first unlock: each platform says so its own way. Android's Keystore
/// throws Java exceptions, which only its build can name.
/// </remarks>
internal static class StoreFailures
{
	/// <summary>
	/// Whether an exception from SecureStorage means "unreadable" rather than a fault to surface.
	/// </summary>
	/// <param name="exception">What SecureStorage threw.</param>
	/// <returns>True when the stored value should be treated as absent.</returns>
	public static bool IsUnreadable(Exception exception) =>
		exception is InvalidOperationException or CryptographicException or System.Security.SecurityException
#if ANDROID
			or Java.Lang.Exception
#endif
		;
}
