// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

// Scenarios run in parallel: each has its own World — its own server, stores
// and client — and nothing here is process-wide, unlike Link's messenger. The
// one shared resource is RSA key generation, which is thread-safe.
[assembly: CollectionBehavior(DisableTestParallelization = false)]
