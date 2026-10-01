// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

using System.Collections.Concurrent;

/// <summary>An <see cref="IEvaluator"/> whose outcome a test scripts, recording every context it was given.</summary>
internal sealed class ScriptedEvaluator: IEvaluator
{
    private readonly Func<EvaluationContext, CancellationToken, ValueTask<EvaluationOutcome>> _script;
    private readonly ConcurrentQueue<EvaluationContext> _contexts = new();

    /// <summary>Initializes an evaluator that passes every case unless scripted otherwise.</summary>
    /// <param name="key">The stable evaluator key.</param>
    /// <param name="script">The outcome factory, or <see langword="null"/> to pass.</param>
    /// <param name="supported">The supported criterion kinds, or <see langword="null"/> to accept any case.</param>
    /// <param name="requiresFixture">Whether a fixture reference is required.</param>
    /// <param name="version">The evaluator version.</param>
    public ScriptedEvaluator(
        string key,
        Func<EvaluationContext, CancellationToken, ValueTask<EvaluationOutcome>>? script = null,
        ImmutableArray<EvaluationCriterionKey>? supported = null,
        bool requiresFixture = false,
        long version = 1)
    {
        Descriptor = new EvaluatorDescriptor(new EvaluatorKey(key), new EvaluatorVersion(version), key, supported ?? [], requiresFixture);
        _script = script ?? ((_, _) => ValueTask.FromResult<EvaluationOutcome>(new EvaluationPassed(EvaluationScore.Certain(true), "scripted pass")));
    }

    /// <inheritdoc/>
    public EvaluatorDescriptor Descriptor { get; }

    /// <summary>Gets every context received, in call order.</summary>
    public IReadOnlyCollection<EvaluationContext> Contexts => _contexts;

    /// <inheritdoc/>
    public ValueTask<EvaluationOutcome> EvaluateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        _contexts.Enqueue(context);
        return _script(context, cancellationToken);
    }
}
