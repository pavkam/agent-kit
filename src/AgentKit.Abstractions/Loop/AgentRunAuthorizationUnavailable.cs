// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The run stopped because the security authority could not capture fresh
/// authorization for one of the run's operations, so no protected work was
/// attempted under that operation.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a fail-closed authority outcome, distinct from
/// <see cref="AgentRunSessionOperationFailed"/> (a session read or append that
/// failed) and from <see cref="AgentRunInvalidState"/> (captured evidence that
/// contradicts the run-start evidence). It corresponds to
/// <see cref="SecurityAuthorizationCaptureUnavailable"/> returned by the
/// selected <see cref="ISecurityProfileSelector"/>. The loop reports it at
/// run start, before any history is loaded, or at the start of a later turn,
/// in which case every message committed by earlier turns remains in the
/// result.
/// </para>
/// </remarks>
public sealed record AgentRunAuthorizationUnavailable: AgentRunOutcome
{
    /// <summary>Initializes a new instance of the <see cref="AgentRunAuthorizationUnavailable"/> record.</summary>
    /// <param name="safeReason">A human-readable, non-sensitive explanation that names no policy content or credential.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeReason"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public AgentRunAuthorizationUnavailable(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    /// <value>Bounded diagnostic text describing why authorization could not be captured.</value>
    public string SafeReason { get; init; }
}
