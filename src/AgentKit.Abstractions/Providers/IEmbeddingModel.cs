// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// One configured embedding model implementation: performs exactly one
/// provider attempt for one <see cref="EmbeddingModelRequest"/> and returns
/// its terminal outcome.
/// </summary>
/// <remarks>
/// <para>
/// This is the embedding-generation analog of <see cref="ILlmModel"/>, not
/// an optional method on it: embedding generation is a separate provider
/// contract with its own alias space, descriptor type, and result shape.
/// A provider package may implement both interfaces for different
/// registered aliases, but the two remain independently selectable and
/// replaceable.
/// </para>
/// <para>
/// Unlike <see cref="ILlmModel"/>, this contract is not streamed: an
/// embedding request/response is not incrementally observable, so there is
/// no observer parameter and no partial-content concept on failure.
/// </para>
/// <para>
/// Selection, safe retry, and fallback across candidate models are owned by
/// a component above this interface, which shares retry budgets and policy
/// across attempts; an implementation performs exactly one attempt per
/// call and does not retry internally.
/// </para>
/// </remarks>
public interface IEmbeddingModel
{
    /// <summary>Gets the application-facing selection key this instance serves.</summary>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "'Alias' is the established AgentKit provider-catalog term for a model " +
            "selection key across every architecture document and the wider provider surface; " +
            "renaming it here alone would make this the only inconsistent member in that vocabulary.")]
    public EmbeddingModelAlias Alias { get; }

    /// <summary>Performs one embedding generation attempt.</summary>
    /// <param name="request">The request to embed.</param>
    /// <param name="cancellationToken">A token used to cancel the attempt.</param>
    /// <returns>The terminal outcome of the attempt.</returns>
    public Task<EmbeddingAttemptResult> GenerateAsync(
        EmbeddingModelRequest request,
        CancellationToken cancellationToken = default);
}
