// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Assesses one case repetition against its expected criteria and reports a typed outcome.</summary>
/// <remarks>
/// <para>
/// An evaluator is an independent capability. It observes only the <see cref="EvaluationContext"/>: the public run result,
/// the case, and recorded evidence. It never receives a loop, run scope, provider SDK, or service provider.
/// </para>
/// <para>
/// Implementations are thread-safe singletons unless registered otherwise: the runner may call
/// <see cref="EvaluateAsync"/> concurrently for different case repetitions. A thrown exception other than cancellation is
/// recorded as <see cref="EvaluatorFaulted"/> and never changes the run result.
/// </para>
/// </remarks>
public interface IEvaluator
{
    /// <summary>Gets the stable descriptor: key, version, supported criteria, and fixture requirement.</summary>
    /// <value>An immutable descriptor that does not change for the life of the instance.</value>
    public EvaluatorDescriptor Descriptor { get; }

    /// <summary>Evaluates one case repetition.</summary>
    /// <param name="context">The immutable case, result, and manifest evidence.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The typed outcome; one of passed, failed, inconclusive, skipped, cancelled, unsupported, or evaluator failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; the runner records a cancelled outcome.</exception>
    public ValueTask<EvaluationOutcome> EvaluateAsync(EvaluationContext context, CancellationToken cancellationToken = default);
}
