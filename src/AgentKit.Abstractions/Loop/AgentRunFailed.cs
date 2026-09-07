// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The run stopped because a model attempt or tool invocation failed unexpectedly.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Any
/// truthful partial output the failed attempt produced is preserved as an
/// interrupted message in durable history before this outcome is returned;
/// it is never presented as this outcome's own content.
/// </remarks>
public sealed record AgentRunFailed: AgentRunOutcome
{
    /// <summary>Initializes a new instance of the <see cref="AgentRunFailed"/> record.</summary>
    /// <param name="failure">The normalized failure that stopped the run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public AgentRunFailed(ProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the normalized failure that stopped the run.</summary>
    public ProviderFailure Failure { get; init; }
}
