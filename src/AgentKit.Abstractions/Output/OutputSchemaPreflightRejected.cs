// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Indicates that bounded schema preflight rejected configuration.</summary>
public sealed record OutputSchemaPreflightRejected: OutputSchemaPreflightResult
{
    /// <summary>Initializes a rejected preflight result.</summary>
    /// <param name="failure">The non-null safe configuration failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public OutputSchemaPreflightRejected(OutputSchemaConfigurationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the configuration failure that prevented preflight.</summary>
    /// <value>Safe, typed failure evidence.</value>
    public OutputSchemaConfigurationFailure Failure { get; }
}
