// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The branch has advanced past the request's expected version. Nothing was
/// appended.
/// </summary>
/// <remarks>
/// The store never rebases the request against the newer version or
/// silently retries; the caller decides whether to reload and reattempt
/// with a fresh expected version.
/// </remarks>
public sealed record SessionAppendConflict: SessionAppendResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionAppendConflict"/> record.</summary>
    /// <param name="expectedVersion">The version the request expected.</param>
    /// <param name="actualVersion">The branch's actual current version.</param>
    public SessionAppendConflict(SessionVersion expectedVersion, SessionVersion actualVersion)
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>Gets the version the request expected.</summary>
    public SessionVersion ExpectedVersion { get; init; }

    /// <summary>Gets the branch's actual current version.</summary>
    public SessionVersion ActualVersion { get; init; }
}
