// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Indicates output processing stopped because selected schema configuration is invalid or unsupported.</summary>
public sealed record OutputConfigurationRejected: OutputProcessingResult
{
    /// <summary>Initializes a non-retriable configuration outcome.</summary>
    /// <param name="failure">The non-null safe configuration failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public OutputConfigurationRejected(OutputSchemaConfigurationFailure failure) { ArgumentNullException.ThrowIfNull(failure); Failure = failure; }
    /// <summary>Gets non-retriable safe failure evidence.</summary><value>A non-null configuration failure.</value>
    public OutputSchemaConfigurationFailure Failure { get; }
}
