// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An agent-definition source whose contribution is fixed at registration
/// time.
/// </summary>
/// <remarks>
/// <para>
/// This is the source applications use when agents are declared in code or
/// bound from configuration at startup. It is immutable and therefore
/// trivially thread-safe.
/// </para>
/// <para>
/// Its version never changes, because its content never changes. A deployment
/// that needs definitions to change at runtime registers a source that
/// re-reads them instead of mutating this one.
/// </para>
/// </remarks>
public sealed class StaticAgentDefinitionSource: IAgentDefinitionSource
{
    private readonly AgentDefinitionSourceSnapshot _snapshot;

    /// <summary>
    /// Initializes a source publishing a fixed set of definitions.
    /// </summary>
    /// <param name="sourceId">This source's stable identity.</param>
    /// <param name="definitions">
    /// The definitions to publish. An empty set is valid.
    /// </param>
    /// <param name="precedence">
    /// This source's precedence when two sources publish the same
    /// <see cref="AgentId"/>. Higher wins.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="definitions"/> is uninitialized, contains
    /// <see langword="null"/>, or contains a duplicate agent identity.
    /// </exception>
    public StaticAgentDefinitionSource(
        AgentDefinitionSourceId sourceId,
        ImmutableArray<AgentDefinition> definitions,
        int precedence = 0)
    {
        SourceId = sourceId;
        _snapshot = new AgentDefinitionSourceSnapshot(
            sourceId,
            new AgentDefinitionSourceVersion(0),
            precedence,
            definitions);
    }

    /// <inheritdoc/>
    public AgentDefinitionSourceId SourceId { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Always completes synchronously and always returns the same immutable
    /// snapshot instance. Cancellation is honored before that return so the
    /// contract behaves consistently with sources that do real work.
    /// </remarks>
    public ValueTask<AgentDefinitionSourceSnapshot> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_snapshot);
    }
}
