// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationExportRecordTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentException>(() => new EvaluationExportRecord(default, new EvaluationExported())).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new EvaluationExportRecord(new EvaluationReportExporterKey("e"), null!)).ParamName.ShouldBe("result");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        var record = new EvaluationExportRecord(new EvaluationReportExporterKey("e"), new EvaluationExported("somewhere"));

        record.Key.ShouldBe(new EvaluationReportExporterKey("e"));
        record.Result.ShouldBe(new EvaluationExported("somewhere"));
    }
}
