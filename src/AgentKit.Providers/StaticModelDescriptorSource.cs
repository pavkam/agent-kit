// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// A descriptor source whose contribution is fixed at registration time.
/// </summary>
/// <remarks>
/// <para>
/// This is the source concrete provider packages and applications use when
/// the set of configured models is known from configuration rather than
/// discovered from a live provider API. It is immutable and therefore
/// trivially thread-safe.
/// </para>
/// <para>
/// Its version never changes, because its content never changes. A
/// deployment that needs live discovery registers a source that performs it
/// instead of mutating this one.
/// </para>
/// </remarks>
public sealed class StaticModelDescriptorSource: IModelDescriptorSource
{
    private readonly ModelDescriptorSourceSnapshot _snapshot;

    /// <summary>
    /// Initializes a source publishing a fixed set of conversational models.
    /// </summary>
    /// <param name="sourceId">This source's stable identity.</param>
    /// <param name="conversationModels">
    /// The descriptors to publish. An empty set is valid.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="conversationModels"/> is uninitialized or contains
    /// <see langword="null"/>.
    /// </exception>
    public StaticModelDescriptorSource(
        ModelDescriptorSourceId sourceId,
        ImmutableArray<ModelDescriptor> conversationModels)
    {
        ArgumentException.ThrowIfContainsNull(conversationModels);

        SourceId = sourceId;
        _snapshot = new ModelDescriptorSourceSnapshot(
            sourceId,
            new ModelDescriptorSourceVersion(0),
            conversationModels);
    }

    /// <inheritdoc/>
    public ModelDescriptorSourceId SourceId { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Always completes synchronously and always returns the same immutable
    /// snapshot instance. Cancellation is honored before that return so the
    /// contract behaves consistently with sources that do real work.
    /// </remarks>
    public ValueTask<ModelDescriptorSourceSnapshot> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_snapshot);
    }
}
