// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>
/// An <see cref="ILlmModel"/> test double that returns a scripted sequence
/// of terminal attempt outcomes, one per call, and records every request it
/// received.
/// </summary>
internal sealed class FakeLlmModel: ILlmModel
{
    private readonly Queue<ModelAttemptResult> _results;

    /// <summary>Initializes a new instance of the <see cref="FakeLlmModel"/> class.</summary>
    /// <param name="alias">The alias this fake serves.</param>
    /// <param name="results">The scripted results to return, one per call, in order.</param>
    public FakeLlmModel(ModelAlias alias, params IEnumerable<ModelAttemptResult> results)
    {
        Alias = alias;
        _results = new Queue<ModelAttemptResult>(results);
    }

    /// <inheritdoc/>
    public ModelAlias Alias { get; }

    /// <summary>Gets every request this fake received, in call order.</summary>
    public List<LlmModelRequest> ReceivedRequests { get; } = [];

    /// <inheritdoc/>
    public Task<ModelAttemptResult> ExecuteAsync(
        LlmModelRequest request, IModelResponseObserver observer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(observer);
        ReceivedRequests.Add(request);

        return _results.Count == 0
            ? throw new InvalidOperationException("This fake has no more scripted results.")
            : Task.FromResult(_results.Dequeue());
    }
}
