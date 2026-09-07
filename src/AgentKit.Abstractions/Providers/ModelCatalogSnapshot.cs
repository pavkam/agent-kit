// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable, versioned set of configured models one selection decision
/// may choose from.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// A snapshot is a stable read. An operation that begins against one version
/// keeps using it for its whole lifetime, so a concurrent catalog reload
/// cannot change which model a run is talking to midway through.
/// </para>
/// <para>
/// The snapshot never contains a union descriptor claiming capabilities that
/// no single configured model actually supports, and it never retains
/// provider SDK clients. It is configuration evidence, not a connection pool.
/// </para>
/// </remarks>
public sealed record ModelCatalogSnapshot
{
    private readonly ImmutableArray<ModelDescriptor> _conversationModels;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelCatalogSnapshot"/>
    /// record.
    /// </summary>
    /// <param name="version">This snapshot's catalog revision.</param>
    /// <param name="conversationModels">
    /// The composed conversational model descriptors. An empty catalog is
    /// structurally valid; rejecting it is a composition-validation concern
    /// rather than a snapshot invariant.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="conversationModels"/> is uninitialized, contains a
    /// <see langword="null"/> element, or contains two descriptors with the
    /// same <see cref="ModelDescriptor.Alias"/>. An ambiguous alias would
    /// make selection non-deterministic.
    /// </exception>
    public ModelCatalogSnapshot(
        ModelCatalogVersion version,
        ImmutableArray<ModelDescriptor> conversationModels)
    {
        ArgumentException.ThrowIfContainsNull(conversationModels);
        ThrowIfDuplicateAlias(conversationModels, nameof(conversationModels));

        Version = version;
        _conversationModels = conversationModels;
    }

    /// <summary>Gets this snapshot's catalog revision.</summary>
    public ModelCatalogVersion Version { get; init; }

    /// <summary>Gets the composed conversational model descriptors.</summary>
    /// <value>
    /// Aliases are unique within this collection, so a lookup by alias always
    /// resolves to at most one descriptor.
    /// </value>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized array, one containing
    /// <see langword="null"/>, or one with a duplicate alias.
    /// </exception>
    public ImmutableArray<ModelDescriptor> ConversationModels
    {
        get => _conversationModels;
        init
        {
            ArgumentException.ThrowIfContainsNull(value, nameof(ConversationModels));
            ThrowIfDuplicateAlias(value, nameof(ConversationModels));
            _conversationModels = value;
        }
    }

    /// <summary>
    /// Finds the conversational descriptor published under
    /// <paramref name="alias"/>.
    /// </summary>
    /// <param name="alias">The application selection key to resolve.</param>
    /// <returns>
    /// The matching descriptor, or <see langword="null"/> when this snapshot
    /// publishes no model under that alias.
    /// </returns>
    /// <remarks>
    /// Returning <see langword="null"/> rather than throwing keeps an
    /// unconfigured alias a selection outcome the caller reports as a typed
    /// diagnostic, not an exception thrown from the middle of a run.
    /// </remarks>
    public ModelDescriptor? FindConversationModel(ModelAlias alias)
    {
        foreach (var descriptor in _conversationModels)
        {
            if (descriptor.Alias == alias)
            {
                return descriptor;
            }
        }

        return null;
    }

    private static void ThrowIfDuplicateAlias(
        ImmutableArray<ModelDescriptor> descriptors,
        string paramName)
    {
        if (descriptors.IsDefaultOrEmpty)
        {
            return;
        }

        var seen = new HashSet<ModelAlias>();
        foreach (var descriptor in descriptors)
        {
            if (!seen.Add(descriptor.Alias))
            {
                throw new ArgumentException(
                    $"Value must not contain duplicate model alias '{descriptor.Alias}'.",
                    paramName);
            }
        }
    }
}
