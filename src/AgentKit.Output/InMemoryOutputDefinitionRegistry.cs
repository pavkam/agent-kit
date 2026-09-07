// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>
/// An immutable <see cref="IOutputDefinitionResolver"/> built once from every
/// additively registered <see cref="OutputDefinition"/>.
/// </summary>
/// <remarks>
/// This type is a thread-safe process singleton reflecting the definitions
/// registered at composition time. When a request does not specify a
/// version, the most recently registered version for that identity is
/// returned. Registering more than one definition with the same identity
/// and version fails construction.
/// </remarks>
internal sealed class InMemoryOutputDefinitionRegistry: IOutputDefinitionResolver
{
    private readonly ImmutableDictionary<OutputDefinitionId, ImmutableArray<OutputDefinition>> _definitions;

    /// <summary>Initializes a new instance of the <see cref="InMemoryOutputDefinitionRegistry"/> class.</summary>
    /// <param name="definitions">Every additively registered output definition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definitions"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="definitions"/> contains more than one definition with the same identity and version.
    /// </exception>
    public InMemoryOutputDefinitionRegistry(IEnumerable<OutputDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var builder = ImmutableDictionary.CreateBuilder<OutputDefinitionId, ImmutableArray<OutputDefinition>>();
        foreach (var definition in definitions)
        {
            var versions = builder.TryGetValue(definition.Id, out var existing) ? existing : [];
            if (versions.Any(v => v.Version.Equals(definition.Version)))
            {
                throw new ArgumentException(
                    $"Definition '{definition.Id}' already has a registered version '{definition.Version}'.",
                    nameof(definitions));
            }

            builder[definition.Id] = versions.Add(definition);
        }

        _definitions = builder.ToImmutable();
    }

    /// <inheritdoc/>
    public ValueTask<OutputDefinitionResult> ResolveAsync(
        OutputDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_definitions.TryGetValue(request.Id, out var versions) || versions.IsEmpty)
        {
            return ValueTask.FromResult<OutputDefinitionResult>(new OutputDefinitionNotFound(request.Id));
        }

        if (request.Version is { } version)
        {
            var match = versions.FirstOrDefault(v => v.Version.Equals(version));
            return ValueTask.FromResult<OutputDefinitionResult>(
                match is not null ? new OutputDefinitionResolved(match) : new OutputDefinitionNotFound(request.Id));
        }

        return ValueTask.FromResult<OutputDefinitionResult>(new OutputDefinitionResolved(versions[^1]));
    }
}
