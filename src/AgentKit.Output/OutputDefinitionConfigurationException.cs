// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>Reports a registered output definition whose schema cannot run under the selected local engine profile.</summary>
public sealed class OutputDefinitionConfigurationException: Exception
{
    /// <summary>Initializes a configuration exception for one definition and schema failure.</summary>
    /// <param name="definitionId">The definition whose schema failed preflight.</param>
    /// <param name="definitionVersion">The exact definition version that failed.</param>
    /// <param name="failure">The non-retriable schema configuration failure.</param>
    /// <exception cref="ArgumentException"><paramref name="definitionId"/> or <paramref name="definitionVersion"/> is default or uninitialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public OutputDefinitionConfigurationException(
        OutputDefinitionId definitionId,
        OutputDefinitionVersion definitionVersion,
        OutputSchemaConfigurationFailure failure)
        : base("An output definition failed local schema preflight.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId.Value, nameof(definitionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionVersion.Value, nameof(definitionVersion));
        ArgumentNullException.ThrowIfNull(failure);
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        Failure = failure;
    }

    /// <summary>Gets the definition identity that failed preflight.</summary>
    /// <value>An initialized output-definition identity.</value>
    public OutputDefinitionId DefinitionId { get; }

    /// <summary>Gets the exact definition version that failed preflight.</summary>
    /// <value>An initialized output-definition version.</value>
    public OutputDefinitionVersion DefinitionVersion { get; }

    /// <summary>Gets the safe non-retriable schema failure.</summary>
    /// <value>The neutral typed failure returned by the selected schema engine.</value>
    public OutputSchemaConfigurationFailure Failure { get; }
}
