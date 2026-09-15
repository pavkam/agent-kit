// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

/// <summary>Verifies the documented <see cref="AgentHookOptions"/> defaults.</summary>
public sealed class AgentHookOptionsTests
{
    [Fact]
    public void MaximumInvocationDepth_WhenDefault_IsEight()
    {
        var options = new AgentHookOptions();

        options.MaximumInvocationDepth.ShouldBe(8);
    }

    [Fact]
    public void MinimumFailureMode_WhenDefault_IsLeastStrictMode()
    {
        // Isolate < FailOperation in strictness, so the default must be Isolate to leave caller modes unchanged.
        var options = new AgentHookOptions();

        options.MinimumFailureMode.ShouldBe(HookFailureMode.Isolate);
    }
}
