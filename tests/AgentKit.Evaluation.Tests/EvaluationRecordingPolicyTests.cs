// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationRecordingPolicyTests
{
    [Fact]
    public void None_WhenRead_PersistsNothingAndExportsNowhere()
    {
        EvaluationRecordingPolicy.None.ResultStore.ShouldBeNull();
        EvaluationRecordingPolicy.None.Exporters.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenStoreKeyIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationRecordingPolicy(default(EvaluationResultStoreKey), []))
            .ParamName.ShouldBe("resultStore");

    [Fact]
    public void Constructor_WhenExportersAreDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationRecordingPolicy(null, default)).ParamName.ShouldBe("exporters");

    [Fact]
    public void Constructor_WhenAnExporterKeyIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationRecordingPolicy(null, [default])).ParamName.ShouldBe("exporters");

    [Fact]
    public void Constructor_WhenAnExporterKeyRepeats_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationRecordingPolicy(null, [new EvaluationReportExporterKey("a"), new EvaluationReportExporterKey("a")]))
            .ParamName.ShouldBe("exporters");

    [Fact]
    public void Equals_WhenExportersMatchInOrder_IsEqualAndHashesAlike()
    {
        var left = new EvaluationRecordingPolicy(new EvaluationResultStoreKey("s"), [new EvaluationReportExporterKey("a"), new EvaluationReportExporterKey("b")]);
        var right = new EvaluationRecordingPolicy(new EvaluationResultStoreKey("s"), [new EvaluationReportExporterKey("a"), new EvaluationReportExporterKey("b")]);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(new EvaluationRecordingPolicy(new EvaluationResultStoreKey("s"), [new EvaluationReportExporterKey("b"), new EvaluationReportExporterKey("a")]));
    }
}
