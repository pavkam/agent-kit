// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves one registered, immutable <see cref="OutputDefinition"/> by identity.</summary>
public interface IOutputDefinitionResolver
{
    /// <summary>Resolves one output definition.</summary>
    /// <param name="request">The resolution request.</param>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>A task producing the closed resolution outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<OutputDefinitionResult> ResolveAsync(
        OutputDefinitionRequest request, CancellationToken cancellationToken = default);
}
