// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of <see cref="DurableOperationResult"/>, the terminal record that settles an operation.</summary>
/// <remarks>
/// Recording this document is what makes an operation unrepeatable: once it is acknowledged, a later writer is refused
/// even when it presents a newer token, because settlement is not a state a durable operation leaves. The persisted
/// failure message is the caller's already-safe text and is never enriched with content, paths, or provider detail on
/// the way into the store.
/// </remarks>
/// <param name="Binding">The non-null persisted address and captured durability context.</param>
/// <param name="State">The terminal state the operation settled in.</param>
/// <param name="SideEffectCertainty">What is provably known about the operation's external effect at settlement.</param>
/// <param name="Output">The non-null versioned serialized terminal output.</param>
/// <param name="FencingToken">The writer token presented at settlement.</param>
/// <param name="CompletedAt">The instant the caller's clock reported at settlement.</param>
/// <param name="SafeFailureMessage">The caller-supplied redaction-safe failure text, or <see langword="null"/> when the operation did not fail.</param>
internal sealed record DurableOperationResultDocument(
    DurableBindingDocument Binding,
    DurableOperationState State,
    SideEffectCertainty SideEffectCertainty,
    OperationPayloadDocument Output,
    long FencingToken,
    DateTimeOffset CompletedAt,
    string? SafeFailureMessage)
{
    /// <summary>Projects one domain terminal record into its portable persisted representation.</summary>
    /// <param name="value">The terminal record to project, or <see langword="null"/> when no terminal record exists yet.</param>
    /// <returns>A document carrying every unwrapped terminal field, or <see langword="null"/> for a null input.</returns>
    internal static DurableOperationResultDocument? FromDomain(DurableOperationResult? value) =>
        value is null
            ? null
            : new DurableOperationResultDocument(
                DurableBindingDocument.FromDomain(value.Binding),
                value.State,
                value.SideEffectCertainty,
                OperationPayloadDocument.FromDomain(value.Output),
                value.FencingToken.Value,
                value.CompletedAt,
                value.SafeFailureMessage);

    /// <summary>Reconstructs the exact domain terminal record this document was projected from.</summary>
    /// <returns>A terminal record equal to the projected original.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Binding"/> or <see cref="Output"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException"><see cref="State"/> is not a terminal state.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enumeration value is undefined or <see cref="FencingToken"/> is not positive.</exception>
    internal DurableOperationResult ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Binding);
        ArgumentNullException.ThrowIfNull(Output);
        return new DurableOperationResult(
            Binding.ToDomain(),
            State,
            SideEffectCertainty,
            Output.ToDomain(),
            new FencingToken(FencingToken),
            CompletedAt,
            SafeFailureMessage);
    }
}
