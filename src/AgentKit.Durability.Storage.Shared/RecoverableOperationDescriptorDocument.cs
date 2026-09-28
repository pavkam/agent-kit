// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of <see cref="RecoverableOperationDescriptor"/>, the declaration committed before any effect starts.</summary>
/// <remarks>
/// A recovering worker classifies an operation from this declaration alone, so every field a recovery policy reads is
/// persisted: who owns retry and the deadline, what cancelling achieves, which effect the operation performs, and
/// whether repeating it is safe. Ownership and idempotency are written as stable enumeration names rather than ordinals
/// under the canonical encoding contract, so reordering an enumeration cannot silently reclassify recorded work.
/// </remarks>
/// <param name="Binding">The non-null persisted address and captured durability context.</param>
/// <param name="Name">The non-blank deterministic operation name.</param>
/// <param name="Version">The non-blank serialized contract version.</param>
/// <param name="IdempotencyKey">The non-blank key an effect owner uses to collapse duplicate attempts.</param>
/// <param name="Input">The non-null versioned serialized input.</param>
/// <param name="RetryOwner">The component responsible for retrying.</param>
/// <param name="TimeoutOwner">The component responsible for enforcing the deadline.</param>
/// <param name="Cancellation">What cancelling the operation actually achieves.</param>
/// <param name="Effect">The material effect the operation performs.</param>
/// <param name="Idempotency">Whether repeating the operation is safe, and on what basis.</param>
/// <param name="Deadline">The instant after which the declared timeout owner considers the operation expired.</param>
/// <param name="CausalParentId">The nonempty parent operation identity, or <see langword="null"/> for a root operation.</param>
/// <param name="Extensions">The ordinal-ordered host-specific extension entries; a default array means none were retained.</param>
internal sealed record RecoverableOperationDescriptorDocument(
    DurableBindingDocument Binding,
    string Name,
    string Version,
    string IdempotencyKey,
    OperationPayloadDocument Input,
    DurableRetryOwner RetryOwner,
    DurableTimeoutOwner TimeoutOwner,
    CancellationSemantics Cancellation,
    SecurityEffect Effect,
    IdempotencyClassification Idempotency,
    DateTimeOffset Deadline,
    Guid? CausalParentId,
    ImmutableArray<ExtensionEntryDocument> Extensions)
{
    /// <summary>Projects one domain declaration into its portable persisted representation.</summary>
    /// <param name="value">The non-null declaration to project.</param>
    /// <returns>A document carrying every unwrapped field and the ordinal-ordered extension entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static RecoverableOperationDescriptorDocument FromDomain(RecoverableOperationDescriptor value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new RecoverableOperationDescriptorDocument(
            DurableBindingDocument.FromDomain(value.Binding),
            value.Name.Value,
            value.Version.Value,
            value.IdempotencyKey.Value,
            OperationPayloadDocument.FromDomain(value.Input),
            value.RetryOwner,
            value.TimeoutOwner,
            value.Cancellation,
            value.Effect,
            value.Idempotency,
            value.Deadline,
            value.CausalParentId?.Value,
            ExtensionEntryDocument.FromDomain(value.Extensions));
    }

    /// <summary>Reconstructs the exact domain declaration this document was projected from.</summary>
    /// <returns>A declaration equal to the projected original, including its extension entries.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Binding"/> or <see cref="Input"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException">A persisted extension entry is malformed, or the binding's authorization cannot describe its address.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A persisted name, version, idempotency key, or parent identity is default, or an enumeration value is undefined.</exception>
    internal RecoverableOperationDescriptor ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Binding);
        ArgumentNullException.ThrowIfNull(Input);
        return new RecoverableOperationDescriptor(
            Binding.ToDomain(),
            new DurableOperationName(Name),
            new DurableOperationVersion(Version),
            new IdempotencyKey(IdempotencyKey),
            Input.ToDomain(),
            RetryOwner,
            TimeoutOwner,
            Cancellation,
            Effect,
            Idempotency,
            Deadline,
            CausalParentId is { } parentId ? new OperationId(parentId) : null,
            ExtensionEntryDocument.ToDomain(Extensions));
    }

    /// <summary>Compares declarations by ordered extension contents rather than by immutable-array storage identity.</summary>
    /// <param name="other">The candidate declaration to compare with this one, which may be null.</param>
    /// <returns><see langword="true"/> when every scalar and nested member is equal and both extension sequences are element-wise equal.</returns>
    /// <remarks>
    /// Compiler-generated record equality compares <see cref="Extensions"/> by backing-array identity, so two documents
    /// decoded from byte-identical JSON would otherwise compare unequal and a store's encoding fidelity probe would
    /// reject a contract that actually round-trips correctly.
    /// </remarks>
    public bool Equals(RecoverableOperationDescriptorDocument? other) =>
        other is not null
        && Binding == other.Binding
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && string.Equals(Version, other.Version, StringComparison.Ordinal)
        && string.Equals(IdempotencyKey, other.IdempotencyKey, StringComparison.Ordinal)
        && Input == other.Input
        && RetryOwner == other.RetryOwner
        && TimeoutOwner == other.TimeoutOwner
        && Cancellation == other.Cancellation
        && Effect == other.Effect
        && Idempotency == other.Idempotency
        && Deadline == other.Deadline
        && CausalParentId == other.CausalParentId
        && Extensions.AsSpan().SequenceEqual(other.Extensions.AsSpan());

    /// <summary>Computes a hash consistent with <see cref="Equals(RecoverableOperationDescriptorDocument?)"/>.</summary>
    /// <returns>A hash derived from every scalar and nested member and each ordered extension entry.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Binding);
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(Version, StringComparer.Ordinal);
        hash.Add(IdempotencyKey, StringComparer.Ordinal);
        hash.Add(Input);
        hash.Add(RetryOwner);
        hash.Add(TimeoutOwner);
        hash.Add(Cancellation);
        hash.Add(Effect);
        hash.Add(Idempotency);
        hash.Add(Deadline);
        hash.Add(CausalParentId);
        foreach (var entry in Extensions.AsSpan())
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }
}
