// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the coordinator to correct one active memory by appending a replacement and marking the original corrected.</summary>
/// <remarks>A correction is itself a proposal: policy evaluates the replacement content before any write, and history is appended rather than rewritten. The correction is a versioned compare-and-set on <see cref="ExpectedVersion"/>.</remarks>
public sealed record MemoryCorrectionRequest
{
    /// <summary>Initializes a validated correction.</summary>
    /// <param name="context">The operation context the correction runs under.</param>
    /// <param name="id">The active memory to correct.</param>
    /// <param name="expectedVersion">The version the stored record must currently have.</param>
    /// <param name="replacementId">The identity of the replacement record.</param>
    /// <param name="content">The corrected content.</param>
    /// <param name="provenance">The source evidence for the correction.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, or the replacement identity equals the corrected one.</exception>
    /// <exception cref="ArgumentException">A version or key is blank.</exception>
    public MemoryCorrectionRequest(
        MemoryOperationContext context,
        MemoryId id,
        VersionToken expectedVersion,
        MemoryId replacementId,
        MemoryContent content,
        Provenance provenance,
        IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedVersion.Value, nameof(expectedVersion));
        ArgumentOutOfRangeException.ThrowIfEqual(replacementId, default, nameof(replacementId));
        ArgumentOutOfRangeException.ThrowIfEqual(replacementId, id, nameof(replacementId));
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Context = context;
        Id = id;
        ExpectedVersion = expectedVersion;
        ReplacementId = replacementId;
        Content = content;
        Provenance = provenance;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the operation context the correction runs under.</summary>
    public MemoryOperationContext Context { get; }

    /// <summary>Gets the active memory to correct.</summary>
    public MemoryId Id { get; }

    /// <summary>Gets the version the stored record must currently have.</summary>
    public VersionToken ExpectedVersion { get; }

    /// <summary>Gets the identity of the replacement record.</summary>
    public MemoryId ReplacementId { get; }

    /// <summary>Gets the corrected content.</summary>
    public MemoryContent Content { get; }

    /// <summary>Gets the source evidence for the correction.</summary>
    public Provenance Provenance { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
