// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies ProcessResolutionResult behavior and contracts.</summary>
public sealed class ProcessResolutionResultTests
{
    [Fact]
    public void ProcessResolutionResult_WhenStatusAndIntentDisagree_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ProcessResolutionResult(ProcessResolutionStatus.Resolved, null, null));
        exception.ParamName.ShouldBe("intent");
    }
}
