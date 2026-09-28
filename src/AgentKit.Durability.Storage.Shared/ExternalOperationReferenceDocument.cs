// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of <see cref="ExternalOperationReference"/>, the handle naming work owned outside this process.</summary>
/// <remarks>
/// An external reference is the only thing a recovering worker can use to ask an external system what actually
/// happened, so it is persisted verbatim. The handle is opaque to AgentKit and is never parsed, normalized, or trimmed
/// on the way through the store.
/// </remarks>
/// <param name="BackendKey">The non-blank key of the backend that owns the referenced work.</param>
/// <param name="Handle">The non-blank backend-defined handle, preserved exactly as the backend issued it.</param>
internal sealed record ExternalOperationReferenceDocument(string BackendKey, string Handle)
{
    /// <summary>Projects one domain external reference into its portable persisted representation.</summary>
    /// <param name="value">The external reference to project, or <see langword="null"/> when the record names no external work.</param>
    /// <returns>A document carrying the unwrapped backend key and handle, or <see langword="null"/> for a null input.</returns>
    internal static ExternalOperationReferenceDocument? FromDomain(ExternalOperationReference? value) =>
        value is null ? null : new ExternalOperationReferenceDocument(value.BackendKey.Value, value.Handle);

    /// <summary>Reconstructs the exact domain external reference this document was projected from.</summary>
    /// <returns>An external reference equal to the projected original.</returns>
    /// <exception cref="ArgumentException"><see cref="Handle"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="BackendKey"/> is blank.</exception>
    internal ExternalOperationReference ToDomain() =>
        new(new DurableBackendKey(BackendKey), Handle);
}
