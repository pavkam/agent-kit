// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Proposes that one piece of content become durable memory.</summary>
/// <remarks>
/// A model or tool may only propose: a proposal is never retention. The coordinator asks the configured memory policy whether
/// the proposal may become durable state, and only an explicit allow reaches a store. A proposal carries the complete
/// operation context, so the decision and any later write are bound to the authenticated identity and exact profile version.
/// </remarks>
public sealed record MemoryProposal
{
    /// <summary>The namespace a proposal targets when it does not name one.</summary>
    public static MemoryNamespace DefaultNamespace { get; } = new("default");

    /// <summary>Initializes a validated proposal.</summary>
    /// <param name="id">The identity the durable record will carry if accepted.</param>
    /// <param name="context">The operation context the proposal was made under.</param>
    /// <param name="kind">The semantic role of the content.</param>
    /// <param name="content">The proposed body.</param>
    /// <param name="classification">The sensitivity of the content.</param>
    /// <param name="provenance">The source evidence for the content.</param>
    /// <param name="retention">The requested retention.</param>
    /// <param name="proposedAt">The instant of the proposal, taken from the injected clock.</param>
    /// <param name="extensions">Additional extension fields.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The identity is default or an enumeration is undefined.</exception>
    public MemoryProposal(
        MemoryId id,
        MemoryOperationContext context,
        MemoryKind kind,
        MemoryContent content,
        DataClassification classification,
        Provenance provenance,
        RetentionPolicy retention,
        DateTimeOffset proposedAt,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfUndefined(classification);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentNullException.ThrowIfNull(extensions);
        Id = id;
        Context = context;
        Kind = kind;
        Content = content;
        Classification = classification;
        Provenance = provenance;
        Retention = retention;
        ProposedAt = proposedAt;
        Extensions = extensions;
    }

    /// <summary>Gets the identity the durable record carries if accepted.</summary>
    public MemoryId Id { get; }

    /// <summary>Gets the operation context the proposal was made under.</summary>
    public MemoryOperationContext Context { get; }

    /// <summary>Gets the semantic role of the content.</summary>
    public MemoryKind Kind { get; }

    /// <summary>Gets the proposed body.</summary>
    public MemoryContent Content { get; }

    /// <summary>Gets the sensitivity of the content.</summary>
    public DataClassification Classification { get; }

    /// <summary>Gets the source evidence for the content.</summary>
    public Provenance Provenance { get; }

    /// <summary>Gets the requested retention.</summary>
    public RetentionPolicy Retention { get; }

    /// <summary>Gets the instant of the proposal.</summary>
    public DateTimeOffset ProposedAt { get; }

    /// <summary>Gets additional extension fields.</summary>
    public ExtensionData Extensions { get; }

    /// <summary>Gets the namespace the record is stored under.</summary>
    /// <value>Defaults to <see cref="DefaultNamespace"/>.</value>
    /// <exception cref="ArgumentException">An initializer supplies a default or blank namespace.</exception>
    public MemoryNamespace Namespace
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(Namespace));
            field = value;
        }
    } = DefaultNamespace;

    /// <summary>Gets a value indicating whether every principal in the tenant may read the record, subject to authorization.</summary>
    /// <value>False by default: a record is private to the proposing principal.</value>
    public bool ShareWithTenant { get; init; }
}
