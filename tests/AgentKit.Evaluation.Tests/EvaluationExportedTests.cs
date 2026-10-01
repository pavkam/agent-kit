// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationExportedTests
{
    [Fact]
    public void Constructor_WhenLocationIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationExported(" ")).ParamName.ShouldBe("location");

    [Fact]
    public void Constructor_WhenLocationIsOmitted_LeavesItNull() =>
        new EvaluationExported().Location.ShouldBeNull();

    [Fact]
    public void Constructor_WhenLocationIsSupplied_PreservesIt() =>
        new EvaluationExported("file:report.json").Location.ShouldBe("file:report.json");
}
