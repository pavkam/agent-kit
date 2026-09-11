// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolUsageMeasurement behavior and contracts.</summary>
public sealed class ToolUsageMeasurementTests
{
    [Fact]
    public void ToolUsageMeasurement_Constructor_WhenQualityAndAmountConflict_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolUsageMeasurement(new BudgetDimension("requests"), new BudgetUnit("count"), null, ToolUsageMeasurementQuality.Measured));
        exception.ParamName.ShouldBe("amount");
    }
}
