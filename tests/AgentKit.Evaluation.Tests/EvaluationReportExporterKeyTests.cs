// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationReportExporterKeyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenValueIsBlank_ThrowsBeforeCreatingTheKey(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new EvaluationReportExporterKey(value!));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueIsSupplied_PreservesTheExactText()
    {
        var key = new EvaluationReportExporterKey("  Mixed-Case  ");

        key.Value.ShouldBe("  Mixed-Case  ");
        key.ToString().ShouldBe("  Mixed-Case  ");
    }

    [Fact]
    public void Equals_WhenTextDiffersOnlyByCase_IsNotEqual()
    {
        new EvaluationReportExporterKey("Key").ShouldNotBe(new EvaluationReportExporterKey("key"));
        new EvaluationReportExporterKey("key").ShouldBe(new EvaluationReportExporterKey("key"));
    }

    [Fact]
    public void ToString_WhenInstanceIsDefault_ReturnsEmptyText()
    {
        default(EvaluationReportExporterKey).ToString().ShouldBeEmpty();
        default(EvaluationReportExporterKey).Value.ShouldBeNull();
    }
}
