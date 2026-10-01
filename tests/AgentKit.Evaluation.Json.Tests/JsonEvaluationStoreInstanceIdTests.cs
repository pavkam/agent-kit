// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json.Tests;

public sealed class JsonEvaluationStoreInstanceIdTests
{
    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonEvaluationStoreInstanceId(Guid.Empty)).ParamName.ShouldBe("value");

    [Fact]
    public void ToString_WhenValueIsSupplied_ReturnsHyphenatedText()
    {
        var value = Guid.NewGuid();

        new JsonEvaluationStoreInstanceId(value).ToString().ShouldBe(value.ToString("D"));
    }
}
