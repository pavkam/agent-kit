// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite.Tests;

public sealed class SqliteEvaluationStoreInstanceIdTests
{
    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteEvaluationStoreInstanceId(Guid.Empty)).ParamName.ShouldBe("value");

    [Fact]
    public void ToString_WhenValueIsSupplied_ReturnsHyphenatedText()
    {
        var value = Guid.NewGuid();

        new SqliteEvaluationStoreInstanceId(value).ToString().ShouldBe(value.ToString("D"));
    }
}
