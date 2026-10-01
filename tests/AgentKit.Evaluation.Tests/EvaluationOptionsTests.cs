// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationOptionsTests
{
    [Fact]
    public void Constructor_WhenCreated_HasTheDocumentedDefaults()
    {
        var options = new EvaluationOptions();

        options.MaximumConcurrentCases.ShouldBe(4);
        options.MaximumRepetitions.ShouldBe(1);
        options.DefaultCaseTimeout.ShouldBe(TimeSpan.FromMinutes(5));
    }
}
