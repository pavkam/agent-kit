// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;


/// <summary>Verifies <see cref="MemoryHookVeto"/> validation.</summary>
public sealed class MemoryHookVetoTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesCodeAndReason()
    {
        var veto = new MemoryHookVeto("hold", "A hold applies.");

        veto.Code.ShouldBe("hold");
        veto.SafeReason.ShouldBe("A hold applies.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenCodeOrReasonIsBlank_ThrowsTheExactArgumentException(string? blank)
    {
        Should.Throw<ArgumentException>(() => new MemoryHookVeto(blank!, "reason")).ParamName.ShouldBe("code");
        Should.Throw<ArgumentException>(() => new MemoryHookVeto("code", blank!)).ParamName.ShouldBe("safeReason");
    }
}
