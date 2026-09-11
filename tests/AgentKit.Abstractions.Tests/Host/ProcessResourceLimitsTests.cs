// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies ProcessResourceLimits behavior and contracts.</summary>
public sealed class ProcessResourceLimitsTests
{
    [Fact]
    public void ProcessResourceLimits_WhenTimeoutIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ProcessResourceLimits(TimeSpan.Zero, 100, TimeSpan.FromSeconds(1)));
        exception.ParamName.ShouldBe("timeout");
    }
}
