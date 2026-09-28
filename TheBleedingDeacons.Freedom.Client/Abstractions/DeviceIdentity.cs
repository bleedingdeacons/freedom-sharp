// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Abstractions;

/// <summary>
/// What a device says about itself at sign-in.
/// </summary>
/// <param name="DeviceId">A stable identifier — <c>ANDROID_ID</c> on Android. Decides which tablet row a sign-in lands on; 8–128 of <c>A-Z a-z 0-9 . _ : -</c>.</param>
/// <param name="Platform">E.g. <c>android</c>.</param>
/// <param name="Label">A name an admin can recognise.</param>
/// <param name="Model">The hardware model.</param>
/// <param name="AppVersion">The app's version.</param>
public sealed record DeviceIdentity(string DeviceId, string Platform, string Label, string Model, string AppVersion);
