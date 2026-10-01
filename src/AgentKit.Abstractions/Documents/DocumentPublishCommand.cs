// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the lifecycle coordinator to chunk, embed, index, and activate one document version.</summary>
/// <remarks>The command carries an operation context rather than a grant: the coordinator selects the authority the context's authorization names and asks for single-use grants bound to each exact store operation.</remarks>
public sealed record DocumentPublishCommand
{
    /// <summary>Initializes a validated command.</summary>
    /// <param name="context">The operation context the publication runs under.</param>
    /// <param name="id">The document identity.</param>
    /// <param name="version">The source version being published.</param>
    /// <param name="text">The full source text to chunk.</param>
    /// <param name="metadata">The document metadata.</param>
    /// <param name="classification">The sensitivity of the document.</param>
    /// <param name="provenance">The source evidence.</param>
    /// <param name="retention">The retention policy.</param>
    /// <param name="shareWithTenant">Whether every principal in the tenant may read the document; otherwise only the publishing principal may.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or <paramref name="classification"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The version, text, or key is blank.</exception>
    public DocumentPublishCommand(
        MemoryOperationContext context,
        DocumentId id,
        DocumentVersion version,
        string text,
        DocumentMetadata metadata,
        DataClassification classification,
        Provenance provenance,
        RetentionPolicy retention,
        bool shareWithTenant,
        IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentOutOfRangeException.ThrowIfUndefined(classification);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Context = context;
        Id = id;
        Version = version;
        Text = text;
        Metadata = metadata;
        Classification = classification;
        Provenance = provenance;
        Retention = retention;
        ShareWithTenant = shareWithTenant;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the operation context the publication runs under.</summary>
    public MemoryOperationContext Context { get; }

    /// <summary>Gets the document identity.</summary>
    public DocumentId Id { get; }

    /// <summary>Gets the source version being published.</summary>
    public DocumentVersion Version { get; }

    /// <summary>Gets the full source text to chunk.</summary>
    public string Text { get; }

    /// <summary>Gets the document metadata.</summary>
    public DocumentMetadata Metadata { get; }

    /// <summary>Gets the sensitivity of the document.</summary>
    public DataClassification Classification { get; }

    /// <summary>Gets the source evidence.</summary>
    public Provenance Provenance { get; }

    /// <summary>Gets the retention policy.</summary>
    public RetentionPolicy Retention { get; }

    /// <summary>Gets a value indicating whether every principal in the tenant may read the document.</summary>
    public bool ShareWithTenant { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
