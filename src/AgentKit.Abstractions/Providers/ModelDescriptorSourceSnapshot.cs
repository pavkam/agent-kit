// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One descriptor source's complete, immutable contribution to the model
/// catalog at a point in time.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// A source contributes its whole set rather than incremental edits. The
/// catalog recomposes from complete snapshots so that a removed model
/// actually disappears, instead of persisting because no source announced its
/// deletion.
/// </para>
/// </remarks>
public sealed record ModelDescriptorSourceSnapshot
{
    private readonly ImmutableArray<ModelDescriptor> _conversationModels;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ModelDescriptorSourceSnapshot"/> record.
    /// </summary>
    /// <param name="sourceId">The contributing source's identity.</param>
    /// <param name="version">The revision of this contribution.</param>
    /// <param name="conversationModels">
    /// The conversational model descriptors this source publishes. An empty
    /// set is valid and means the source currently contributes nothing.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="conversationModels"/> is uninitialized or contains a
    /// <see langword="null"/> element.
    /// </exception>
    public ModelDescriptorSourceSnapshot(
        ModelDescriptorSourceId sourceId,
        ModelDescriptorSourceVersion version,
        ImmutableArray<ModelDescriptor> conversationModels)
    {
        ArgumentException.ThrowIfContainsNull(conversationModels);

        SourceId = sourceId;
        Version = version;
        _conversationModels = conversationModels;
    }

    /// <summary>Gets the contributing source's identity.</summary>
    public ModelDescriptorSourceId SourceId { get; init; }

    /// <summary>Gets the revision of this contribution.</summary>
    public ModelDescriptorSourceVersion Version { get; init; }

    /// <summary>Gets the conversational model descriptors this source publishes.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized array or one
    /// containing <see langword="null"/>.
    /// </exception>
    public ImmutableArray<ModelDescriptor> ConversationModels
    {
        get => _conversationModels;
        init
        {
            ArgumentException.ThrowIfContainsNull(value, nameof(ConversationModels));
            _conversationModels = value;
        }
    }
}
