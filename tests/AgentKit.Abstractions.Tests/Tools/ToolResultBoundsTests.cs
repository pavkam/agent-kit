// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolResultBounds behavior and contracts.</summary>
public sealed class ToolResultBoundsTests
{
    [Fact]
    public void ToolResultBounds_Constructor_WhenBoundaryPositive_RetainsLimits()
    {
        var bounds = new ToolResultBounds(1, 1);
        bounds.MaximumCanonicalBytes.ShouldBe(1);
        bounds.MaximumParts.ShouldBe(1);
    }
}
