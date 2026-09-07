// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Performs bounded local schema preflight and candidate evaluation for one immutable profile.</summary>
/// <remarks>
/// Implementations are safe for concurrent callers, perform no implicit external I/O, and capture no mutable global
/// registries. Cancellation and failure return no partial preflight evidence.
/// </remarks>
public interface IOutputSchemaEngine
{
    /// <summary>Gets the immutable dialect and vocabulary capabilities used by every operation.</summary>
    /// <value>A stable profile whose identity and version do not change for the engine lifetime.</value>
    public OutputSchemaEngineProfile Profile { get; }

    /// <summary>Preflights a schema without implicit I/O or mutable global state.</summary>
    /// <param name="request">The non-null bounded schema request.</param>
    /// <param name="cancellationToken">Propagates cancellation before or during local work.</param>
    /// <returns>A closed acceptance or configuration-rejection outcome; cancellation returns no manifest.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public OutputSchemaPreflightResult Preflight(OutputSchemaPreflightRequest request, CancellationToken cancellationToken = default);

    /// <summary>Evaluates a candidate and revalidates captured preflight evidence.</summary>
    /// <param name="request">The non-null bounded evaluation request.</param>
    /// <param name="cancellationToken">Propagates cancellation before or during local work.</param>
    /// <returns>A closed pass, invalid-candidate, or configuration-rejection outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public OutputSchemaEvaluationResult Evaluate(OutputSchemaEvaluationRequest request, CancellationToken cancellationToken = default);
}
