// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the bounded settlement attempt left durable work requiring recovery.</summary>
/// <remarks>The immutable evidence is descriptive; its owner is responsible for safe normalization and actual lifecycle transitions.</remarks>
public sealed record RunSettlementRecoveryRequired: RunSettlementOutcome
{
    /// <summary>Captures a nonnull normalized reason without performing further work.</summary>
    /// <param name="failure">The nonnull immutable evidence to preserve.</param>
    /// <exception cref="ArgumentNullException">The evidence is null.</exception>
    public RunSettlementRecoveryRequired(AgentError failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }
    /// <summary>Gets the original typed evidence without replacing its correlation or effect certainty.</summary>
    /// <value>The nonnull immutable reason captured at construction.</value>
    public AgentError Failure { get; }
}
