// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>
/// An <see cref="IRunContinuationPolicy"/> test double that answers each evaluation through a caller-supplied
/// script and records every context it was offered, so tests can assert both what the loop asked and that the
/// loop obeyed the answer.
/// </summary>
internal sealed class ScriptedRunContinuationPolicy: IRunContinuationPolicy
{
    private readonly Func<RunContinuationContext, RunContinuationDecision> _decide;

    /// <summary>Initializes a new instance of the <see cref="ScriptedRunContinuationPolicy"/> class.</summary>
    /// <param name="decide">Produces the decision for each offered context.</param>
    public ScriptedRunContinuationPolicy(Func<RunContinuationContext, RunContinuationDecision> decide) => _decide = decide;

    /// <summary>Gets every context offered to this policy, in evaluation order.</summary>
    public List<RunContinuationContext> Contexts { get; } = [];

    /// <inheritdoc/>
    public ValueTask<RunContinuationDecision> DecideAsync(RunContinuationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        Contexts.Add(context);
        return ValueTask.FromResult(_decide(context));
    }
}
