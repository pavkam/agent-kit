// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Chooses one configured model for a request from the policy's candidates
/// and the catalog snapshot.
/// </summary>
/// <remarks>
/// <para>
/// Selection is a pure decision. An implementation MUST NOT perform provider
/// I/O, resolve credentials, mint or consume a security grant, or mutate
/// agent state. Choosing a model must never be capable of authorizing
/// reaching one.
/// </para>
/// <para>
/// Implementations must be thread-safe and are normally registered as keyed
/// singletons so different agent definitions can select different strategies.
/// </para>
/// <para>
/// The contract returns <see cref="ValueTask{TResult}"/> because a stable
/// catalog and deterministic policy normally complete synchronously. A
/// selector that consults load or health may await injected services, but any
/// non-determinism must be declared in the decision's reason and diagnostics
/// so the choice remains explainable.
/// </para>
/// <para>
/// A selector never reorders the policy's candidates by inferred quality,
/// price, or popularity. Candidate order is the application's stated
/// preference.
/// </para>
/// </remarks>
public interface IModelSelector
{
    /// <summary>
    /// Chooses a model, or explains why none can be chosen.
    /// </summary>
    /// <param name="request">
    /// The policy, requirements, and catalog snapshot to decide from.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the decision.</param>
    /// <returns>
    /// <see cref="ModelSelected"/> with the chosen descriptor,
    /// <see cref="NoCompatibleModel"/> when the policy was coherent but
    /// nothing matched, or <see cref="InvalidModelPolicy"/> when the policy
    /// could never match.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<ModelSelectionResult> SelectAsync(
        ModelSelectionRequest request,
        CancellationToken cancellationToken = default);
}
