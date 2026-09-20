// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

public sealed class DefaultHookProfileSelectorTests
{
    private readonly DefaultHookProfileSelector _selector = new();

    [Fact]
    public async Task SelectAsync_WhenRequestedProfileNull_ReturnsDefault()
    {
        var result = await _selector.SelectAsync(new HookProfileSelectionRequest(null, null), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<HookProfileSelected>().ProfileKey.ShouldBe(HookProfileOptions.DefaultProfileKey);
    }

    [Fact]
    public async Task SelectAsync_WhenRequestedDefault_ReturnsDefault()
    {
        var result = await _selector.SelectAsync(
            new HookProfileSelectionRequest(HookProfileOptions.DefaultProfileKey, null),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<HookProfileSelected>().ProfileKey.ShouldBe(HookProfileOptions.DefaultProfileKey);
    }

    [Fact]
    public async Task SelectAsync_WhenRequestedUnknown_ReturnsUnavailable()
    {
        var requested = new HookProfileKey("unknown");
        var result = await _selector.SelectAsync(new HookProfileSelectionRequest(requested, null), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<HookProfileUnavailable>().RequestedProfile.ShouldBe(requested);
    }
}
