// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>No definition is registered for the requested identity and version.</summary>
public sealed record OutputDefinitionNotFound: OutputDefinitionResult
{
    /// <summary>Initializes a new instance of the <see cref="OutputDefinitionNotFound"/> record.</summary>
    /// <param name="id">The requested definition identity.</param>
    public OutputDefinitionNotFound(OutputDefinitionId id) => Id = id;

    /// <summary>Gets the requested definition identity.</summary>
    public OutputDefinitionId Id { get; init; }
}
