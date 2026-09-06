// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Performs exactly one logical conversational provider attempt: wire
/// translation, transport, authentication injection, stream parsing, and
/// normalized failure mapping for the model identified by
/// <see cref="Alias"/>.
/// </summary>
/// <remarks>
/// An implementation owns one provider attempt only. It never selects
/// another model, retries a visible stream, executes an application tool,
/// or mutates history; same-model retry, fallback, and history are owned by
/// components above this contract.
/// </remarks>
public interface IChatModel
{
    /// <summary>Gets the application-facing alias this instance serves.</summary>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "'Alias' is the established AgentKit provider-catalog term for a model " +
            "selection key across every architecture document and the wider provider surface; " +
            "renaming it here alone would make this the only inconsistent member in that vocabulary.")]
    public ModelAlias Alias { get; }

    /// <summary>
    /// Executes one attempt, delivering its ordered event sequence to
    /// <paramref name="observer"/> and returning the same terminal outcome
    /// as the last event delivered.
    /// </summary>
    /// <param name="request">The request to execute.</param>
    /// <param name="observer">The observer that receives the ordered response event sequence.</param>
    /// <param name="cancellationToken">A token used to cancel the attempt.</param>
    /// <returns>The terminal outcome of the attempt.</returns>
    public Task<ModelAttemptResult> ExecuteAsync(
        ChatModelRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default);
}
