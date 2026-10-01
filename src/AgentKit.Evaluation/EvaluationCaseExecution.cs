// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Carries the authenticated identity and expected session profile a case runs under.</summary>
/// <remarks>The identity is already authenticated at a trusted ingress and is propagated unchanged; it never grants authority. The runner verifies that the resolved agent definition selects <see cref="SessionProfile"/> before it creates the case session, and the engine revalidates the identity at admission.</remarks>
public sealed record EvaluationCaseExecution
{
    /// <summary>Initializes a validated execution declaration.</summary>
    /// <param name="identity">The authenticated execution identity.</param>
    /// <param name="sessionProfile">The non-blank session profile the agent definition is expected to select.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sessionProfile"/> is default or blank.</exception>
    public EvaluationCaseExecution(ExecutionIdentity identity, SessionProfileKey sessionProfile)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionProfile.Value, nameof(sessionProfile));
        Identity = identity;
        SessionProfile = sessionProfile;
    }

    /// <summary>Gets the authenticated execution identity.</summary>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the session profile the agent definition is expected to select.</summary>
    public SessionProfileKey SessionProfile { get; }
}
