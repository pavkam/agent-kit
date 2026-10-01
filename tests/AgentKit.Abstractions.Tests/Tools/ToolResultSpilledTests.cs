// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies <see cref="ToolResultSpilled"/> requires its committed reference.</summary>
public sealed class ToolResultSpilledTests
{
    [Fact]
    public void Constructor_WhenTheReferenceIsNull_ThrowsNamingIt() =>
        Should.Throw<ArgumentNullException>(() => new ToolResultSpilled(null!)).ParamName.ShouldBe("reference");
}
