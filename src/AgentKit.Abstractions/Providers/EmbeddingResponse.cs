// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The committed, terminal result of one successful <see cref="IEmbeddingModel"/>
/// attempt, carried by <see cref="EmbeddingAttemptCompleted"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// <see cref="Items"/> has exactly one <see cref="EmbeddingItemOutcome"/>
/// per input in the originating <see cref="EmbeddingRequest.Inputs"/>
/// array, in that same order; job completion at the transport level never
/// implies every individual item succeeded, so a caller must inspect each
/// item's own outcome kind.
/// </para>
/// </remarks>
public sealed record EmbeddingResponse
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingResponse"/> record.</summary>
    /// <param name="items">The per-input outcomes, one per originating input, in input order.</param>
    /// <param name="usage">Usage evidence for this response, retaining the provider report's lifecycle state.</param>
    /// <param name="providerRequestId">The provider-supplied request correlation identifier, when available.</param>
    /// <param name="extensions">Provider-specific response metadata.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="usage"/> or <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="items"/> is a default, uninitialized array, is
    /// empty, or contains a null element.
    /// </exception>
    public EmbeddingResponse(
        ImmutableArray<EmbeddingItemOutcome> items,
        ModelUsage usage,
        ProviderRequestId? providerRequestId,
        ExtensionData extensions)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(items);
        ArgumentException.ThrowIfContainsNull(items);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(extensions);

        Items = items;
        Usage = usage;
        ProviderRequestId = providerRequestId;
        Extensions = extensions;
    }

    /// <summary>Gets the per-input outcomes, one per originating input, in input order.</summary>
    public ImmutableArray<EmbeddingItemOutcome> Items { get; init; }

    /// <summary>Gets usage evidence for this response with its independent provider report state.</summary>
    /// <value>A non-null report preserving unknown fields as null and reported zero as a known value.</value>
    public ModelUsage Usage { get; init; }

    /// <summary>Gets the provider-supplied request correlation identifier, when available.</summary>
    public ProviderRequestId? ProviderRequestId { get; init; }

    /// <summary>Gets provider-specific response metadata.</summary>
    public ExtensionData Extensions { get; init; }

    /// <inheritdoc/>
    public bool Equals(EmbeddingResponse? other) =>
        other is not null
        && Items.SequenceEqual(other.Items)
        && Usage.Equals(other.Usage)
        && ProviderRequestId.Equals(other.ProviderRequestId)
        && Extensions.Equals(other.Extensions);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        hash.Add(Usage);
        hash.Add(ProviderRequestId);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
