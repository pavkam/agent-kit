// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationResultCursorTests
{
    [Fact]
    public void Constructor_WhenPositionIsOutOfRange_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationResultCursor(-1, 1)).ParamName.ShouldBe("caseOrdinal");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationResultCursor(0, 0)).ParamName.ShouldBe("repetition");
    }

    [Fact]
    public void Constructor_WhenBoundaryPositionIsSupplied_PreservesIt()
    {
        var cursor = new EvaluationResultCursor(0, 1);

        (cursor.CaseOrdinal, cursor.Repetition).ShouldBe((0, 1));
    }
}
