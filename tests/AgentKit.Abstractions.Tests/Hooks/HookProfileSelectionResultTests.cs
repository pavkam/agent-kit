// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookProfileSelectionResultTests
{
    [Fact]
    public void SelectedConstructor_WhenProfileKeyIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookProfileSelected(default));
        exception.ParamName.ShouldBe("profileKey");
    }

    [Fact]
    public void SelectedConstructor_WhenValid_RoundTripsProfileKey() =>
        new HookProfileSelected(HookKernelTestData.Profile).ProfileKey.ShouldBe(HookKernelTestData.Profile);

    [Fact]
    public void UnavailableConstructor_WhenRequestedProfileIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookProfileUnavailable(default));
        exception.ParamName.ShouldBe("requestedProfile");
    }

    [Fact]
    public void UnavailableConstructor_WhenValid_RoundTripsRequestedProfile() =>
        new HookProfileUnavailable(HookKernelTestData.Profile).RequestedProfile.ShouldBe(HookKernelTestData.Profile);

    [Fact]
    public void Hierarchy_IsClosedToSelectedAndUnavailable()
    {
        HookProfileSelectionResult selected = new HookProfileSelected(HookKernelTestData.Profile);
        HookProfileSelectionResult unavailable = new HookProfileUnavailable(HookKernelTestData.Profile);

        _ = selected.ShouldBeOfType<HookProfileSelected>();
        _ = unavailable.ShouldBeOfType<HookProfileUnavailable>();
    }
}
