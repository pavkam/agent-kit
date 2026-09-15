// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="ILlmModel"/> test double that returns a scripted sequence of terminal attempt outcomes, one per
/// call, records every request it received, and optionally defers to a callback for cancellation or fault tests.
/// </summary>
/// <remarks>
/// Calls are served in order from the scripted queue; a call beyond the script throws
/// <see cref="InvalidOperationException"/> so a test cannot silently pass on an unexpected extra attempt. When
/// <see cref="ExecuteOverride"/> is set it replaces the queue entirely for every call.
/// </remarks>
public sealed class ScriptedLlmModel: ILlmModel
{
    private readonly Queue<ModelAttemptResult> _results;

    /// <summary>Initializes a new instance of the <see cref="ScriptedLlmModel"/> class.</summary>
    /// <param name="alias">The alias this fake serves.</param>
    /// <param name="results">The scripted results to return, one per call, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    public ScriptedLlmModel(ModelAlias alias, params IEnumerable<ModelAttemptResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        Alias = alias;
        _results = new Queue<ModelAttemptResult>(results);
    }

    /// <inheritdoc/>
    public ModelAlias Alias { get; }

    /// <summary>Gets every request this fake received, in call order, including calls served by <see cref="ExecuteOverride"/>.</summary>
    public List<LlmModelRequest> ReceivedRequests { get; } = [];

    /// <summary>
    /// Gets or sets a callback that, when non-null, produces every result instead of the scripted queue; use it to
    /// observe the cancellation token, throw, or delay.
    /// </summary>
    public Func<LlmModelRequest, CancellationToken, Task<ModelAttemptResult>>? ExecuteOverride { get; set; }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The scripted results are exhausted and no override is set.</exception>
    public Task<ModelAttemptResult> ExecuteAsync(
        LlmModelRequest request, IModelResponseObserver observer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(observer);
        ReceivedRequests.Add(request);

        return ExecuteOverride is { } execute
            ? execute(request, cancellationToken)
            : _results.Count == 0
                ? throw new InvalidOperationException("This fake has no more scripted results.")
                : Task.FromResult(_results.Dequeue());
    }
}
