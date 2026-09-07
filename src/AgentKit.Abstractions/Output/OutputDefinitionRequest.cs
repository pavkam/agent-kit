// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One request to resolve a registered <see cref="OutputDefinition"/> by identity.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record OutputDefinitionRequest
{
    /// <summary>Initializes a new instance of the <see cref="OutputDefinitionRequest"/> record.</summary>
    /// <param name="id">The identity of the definition to resolve.</param>
    /// <param name="version">
    /// The exact version to resolve, when the caller requires one;
    /// <see langword="null"/> resolves the latest registered version.
    /// </param>
    public OutputDefinitionRequest(OutputDefinitionId id, OutputDefinitionVersion? version)
    {
        Id = id;
        Version = version;
    }

    /// <summary>Gets the identity of the definition to resolve.</summary>
    public OutputDefinitionId Id { get; init; }

    /// <summary>
    /// Gets the exact version to resolve, when the caller requires one;
    /// <see langword="null"/> resolves the latest registered version.
    /// </summary>
    public OutputDefinitionVersion? Version { get; init; }
}
