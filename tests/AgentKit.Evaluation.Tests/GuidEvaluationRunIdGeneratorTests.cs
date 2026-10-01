// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class GuidEvaluationRunIdGeneratorTests
{
    [Fact]
    public void Create_WhenCalledRepeatedly_ReturnsDistinctNonDefaultIdentities()
    {
        var generator = new GuidEvaluationRunIdGenerator();

        var identities = Enumerable.Range(0, 64).Select(_ => generator.Create()).ToArray();

        identities.ShouldAllBe(static id => id != default);
        identities.Distinct().Count().ShouldBe(identities.Length);
    }

    [Fact]
    public void Create_WhenCalledFromManyThreads_NeverRepeatsAnIdentity()
    {
        var generator = new GuidEvaluationRunIdGenerator();
        var identities = new System.Collections.Concurrent.ConcurrentBag<EvaluationRunId>();

        _ = Parallel.For(0, 256, _ => identities.Add(generator.Create()));

        identities.Distinct().Count().ShouldBe(256);
    }
}
