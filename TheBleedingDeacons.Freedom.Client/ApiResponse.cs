// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Net;
using TheBleedingDeacons.Freedom.Models;

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// One call's answer: data, an error in the server's words, or no answer at all.
/// </summary>
/// <typeparam name="T">The data's type.</typeparam>
public sealed class ApiResponse<T>
	where T : class
{
	/// <summary>Gets a value indicating whether the call succeeded (2xx, or 304 for a manifest).</summary>
	public bool Success { get; init; }

	/// <summary>Gets the data. Null on failure, and on a 304.</summary>
	public T? Data { get; init; }

	/// <summary>Gets the server's error, when it sent one.</summary>
	public ApiError? Error { get; init; }

	/// <summary>Gets the HTTP status, or null when the server could not be reached.</summary>
	public HttpStatusCode? StatusCode { get; init; }

	/// <summary>Gets a value indicating whether the server answered 304 Not Modified.</summary>
	public bool NotModified => StatusCode == HttpStatusCode.NotModified;

	/// <summary>Gets a value indicating whether no answer came back at all: no network, a timeout, a refused connection.</summary>
	public bool Unreachable => StatusCode is null;
}
