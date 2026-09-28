// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace TheBleedingDeacons.Freedom.Client.Maui;

/// <summary>
/// This device, identified by <c>Settings.Secure.ANDROID_ID</c>.
/// </summary>
/// <remarks>
/// <para><b>Why ANDROID_ID.</b> It survives a reinstall, so a device that is
/// reinstalled and signs in again lands on its old row on the server and keeps
/// the overrides an admin gave it. On Android 8 and later it is scoped to the
/// app's signing key and the user, so it is no use to anybody tracking the
/// device across apps — and the server stores only a keyed hash of it anyway.</para>
/// <para><b>What that costs.</b> A debug build and a release build are signed
/// differently, so on one tablet they are two devices. A factory reset makes a
/// new one. And it is the app's word, not proof: the Google account is the proof.</para>
/// </remarks>
public sealed class AndroidDeviceIdentity : IDeviceIdentity
{
	/// <inheritdoc/>
	public Task<DeviceIdentity> GetAsync(CancellationToken cancellationToken)
	{
		var androidId = Settings.Secure.GetString(Android.App.Application.Context.ContentResolver, Settings.Secure.AndroidId) ?? string.Empty;

		var label = DeviceInfo.Current.Name;
		if (string.IsNullOrWhiteSpace(label))
		{
			label = DeviceInfo.Current.Model;
		}

		return Task.FromResult(new DeviceIdentity(
			androidId,
			"android",
			label ?? string.Empty,
			$"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}".Trim(),
			AppInfo.Current.VersionString));
	}
}
