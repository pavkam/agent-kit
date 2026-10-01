// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies StructuredGoalResult constraints.</summary>
public sealed class StructuredGoalResultTests
{
    [Fact]
    public void Constructor_WhenSummaryIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new StructuredGoalResult(" ", ExtensionData.Empty)).ParamName.ShouldBe("summary");

    [Fact]
    public void Constructor_WhenDataIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new StructuredGoalResult("s", null!)).ParamName.ShouldBe("data");
}
