// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Indicates that evaluation rejected mismatched or unavailable configuration.</summary>
public sealed record OutputSchemaEvaluationConfigurationRejected: OutputSchemaEvaluationResult
{
    /// <summary>Initializes an evaluation configuration rejection.</summary>
    /// <param name="failure">The non-null safe configuration failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public OutputSchemaEvaluationConfigurationRejected(OutputSchemaConfigurationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the failure that prevented candidate evaluation.</summary>
    /// <value>Safe, typed failure evidence.</value>
    public OutputSchemaConfigurationFailure Failure { get; }
}
