// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The run halted because a session read or append operation the loop
/// depends on did not succeed: the branch was not found, an append
/// conflicted with a concurrent writer, or the configured store reported a
/// failure.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. This
/// outcome is distinct from <see cref="AgentRunFailed"/>, which reports a
/// failed model provider attempt; a session operation failure occurs before
/// or after a model attempt and never carries a <see cref="ProviderFailure"/>.
/// </remarks>
public sealed record AgentRunSessionOperationFailed: AgentRunOutcome
{
    /// <summary>Initializes a new instance of the <see cref="AgentRunSessionOperationFailed"/> record.</summary>
    /// <param name="safeMessage">A safe, non-sensitive description of the failure.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public AgentRunSessionOperationFailed(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a safe, non-sensitive description of the failure.</summary>
    public string SafeMessage { get; init; }
}
