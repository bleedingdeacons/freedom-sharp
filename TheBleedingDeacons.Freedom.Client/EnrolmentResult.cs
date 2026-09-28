// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// The outcome of <see cref="FreedomClient.EnrolAsync"/>.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Code">The server's error code on a refusal, e.g. <c>freedom_not_authorised</c> or <c>not_authorised</c>.</param>
/// <param name="Message">The server's words on a refusal, fit to show; otherwise a sentence for a log.</param>
/// <param name="Sync">The first sync after signing in, when it got that far.</param>
public sealed record EnrolmentResult(EnrolmentStatus Status, string? Code, string Message, SyncResult? Sync)
{
	/// <summary>Gets a value indicating whether the device is now signed in.</summary>
	public bool Succeeded => Status is EnrolmentStatus.Enrolled or EnrolmentStatus.Reattached;
}
