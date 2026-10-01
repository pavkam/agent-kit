// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ModelRequestJudgeClientOptionsTests
{
    [Fact]
    public void Constructor_WhenCreated_HasTheDocumentedDefaults()
    {
        var options = new ModelRequestJudgeClientOptions();

        options.RequestTimeout.ShouldBe(TimeSpan.FromMinutes(2));
        options.MaximumOutputTokens.ShouldBe(256);
        options.Temperature.ShouldBe(0d);
    }
}
