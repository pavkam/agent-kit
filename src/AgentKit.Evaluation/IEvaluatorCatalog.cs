// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Resolves registered evaluators by their stable key.</summary>
/// <remarks>The first-party catalog resolves keyed registrations from the composition that owns the engine. A replacement must be immutable for the life of a plan run and never choose among conflicting registrations.</remarks>
public interface IEvaluatorCatalog
{
    /// <summary>Finds the evaluator registered under a key.</summary>
    /// <param name="key">The non-blank evaluator key.</param>
    /// <returns>The evaluator, or <see langword="null"/> when none is registered.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    public IEvaluator? Find(EvaluatorKey key);
}
