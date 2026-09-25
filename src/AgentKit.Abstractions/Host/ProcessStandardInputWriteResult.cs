// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The base for standard-input write outcomes on an open process handle.</summary>
public abstract record ProcessStandardInputWriteResult;

/// <summary>The host accepted and wrote the supplied standard-input bytes.</summary>
/// <param name="BytesWritten">The number of bytes written.</param>
public sealed record ProcessStandardInputWriteSucceeded(int BytesWritten): ProcessStandardInputWriteResult;

/// <summary>The write failed because the handle is disposed or standard input is already completed.</summary>
/// <param name="SafeMessage">A content-free reason safe for callers and diagnostics.</param>
public sealed record ProcessStandardInputWriteUnavailable(string SafeMessage): ProcessStandardInputWriteResult;

/// <summary>The write would exceed the authorized or configured standard-input bound.</summary>
/// <param name="SafeMessage">A content-free reason safe for callers and diagnostics.</param>
public sealed record ProcessStandardInputWriteLimitExceeded(string SafeMessage): ProcessStandardInputWriteResult;
