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

    [Fact]
    public void ProcessResolutionResult_WhenNonResolvedHasNoSafeMessage_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessResolutionResult(ProcessResolutionStatus.Failed, null, null)).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void ProcessResolutionResult_WhenResolved_RoundTripsProperties()
    {
        var intent = HostTestData.ResolvedIntent();
        var result = new ProcessResolutionResult(ProcessResolutionStatus.Resolved, intent, null);
        result.Status.ShouldBe(ProcessResolutionStatus.Resolved);
        result.Intent.ShouldBeSameAs(intent);
        result.SafeMessage.ShouldBeNull();
    }

    [Fact]
    public void ProcessResolutionResult_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ProcessResolutionResult(ProcessResolutionStatus.Resolved, HostTestData.ResolvedIntent(), null);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
