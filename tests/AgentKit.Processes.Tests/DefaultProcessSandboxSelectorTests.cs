// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

/// <summary>Verifies <see cref="DefaultProcessSandboxSelector"/> registration lookup.</summary>
public sealed class DefaultProcessSandboxSelectorTests
{
    [Fact]
    public async Task SelectAsync_WhenProviderRegistered_ReturnsSelectedProvider()
    {
        var profile = new SandboxProfileId("test-profile");
        var provider = new FakeProcessSandboxProvider(profile, static _ => new ProcessSandboxResult(ProcessSandboxStatus.Unavailable, null, "unused"));
        var selector = new DefaultProcessSandboxSelector([provider]);

        var result = await selector.SelectAsync(profile, TestContext.Current.CancellationToken);

        var selected = result.ShouldBeOfType<ProcessSandboxSelected>();
        selected.ProfileId.ShouldBe(profile);
        selected.Provider.ShouldBeSameAs(provider);
    }

    [Fact]
    public async Task SelectAsync_WhenProviderMissing_ReturnsMissingOutcome()
    {
        var selector = new DefaultProcessSandboxSelector([]);

        var result = await selector.SelectAsync(new SandboxProfileId("missing"), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ProcessSandboxMissing>();
    }
}
