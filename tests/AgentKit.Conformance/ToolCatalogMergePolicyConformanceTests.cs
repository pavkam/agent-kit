// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Checks catalog policy configured to accept unambiguous contributions and reject unconfigured collisions.</summary>
public abstract class ToolCatalogMergePolicyConformanceTests
{
    /// <summary>Creates the policy through its public registration surface with no collision precedence configured.</summary>
    /// <returns>A concurrently callable policy; the concrete fixture owns its host lifetime.</returns>
    protected abstract IToolCatalogMergePolicy CreatePolicy();

    /// <summary>Selection preserves exact source, descriptor, policy, and alias evidence.</summary>
    [Fact]
    public async Task ResolveAsync_WhenUnambiguous_SelectsOnlyCapturedEvidence()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var context = new ToolCatalogMergeContext(ToolCatalogMergeTestData.Request([candidate.Toolset]), [candidate], []);
        var decision = (await CreatePolicy().ResolveAsync(context, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolCatalogSelection>();
        decision.Tools.ShouldBe([candidate]);
        decision.Aliases.Count.ShouldBe(1);
        decision.Aliases[new ToolAlias("read")].ShouldBe(candidate);
        decision.Aliases.ContainsKey(new ToolAlias(candidate.Tool.Id.Value)).ShouldBeFalse();
    }

    /// <summary>An empty publication remains a valid empty exposure.</summary>
    [Fact]
    public async Task ResolveAsync_WhenCatalogEmpty_SelectsNoToolsOrAliases()
    {
        var selection = (await CreatePolicy().ResolveAsync(new ToolCatalogMergeContext(ToolCaptureTestData.Discovery(), [], []), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolCatalogSelection>();
        selection.Tools.ShouldBeEmpty();
        selection.Aliases.ShouldBeEmpty();
    }

    /// <summary>Every closed collision case rejects in the absence of explicit host precedence.</summary>
    [Fact]
    public async Task ResolveAsync_WhenCollisionsUnconfigured_RejectsCompleteExposure()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("other", "source.other");
        var missing = new ToolAliasAssignment(new ToolAlias("missing"), new ToolIdentity(new ToolId("missing"), new ToolVersion("1")));
        var toolset = ToolCatalogMergeTestData.Toolset("missing", [first.Source], [missing]);
        ToolCatalogCollision[] collisions = [new ToolCatalogIdentityCollision([first, second]), new ToolCatalogAliasCollision(new ToolAlias("read"), [first, second]), new ToolCatalogMissingAliasTarget(toolset, missing)];
        var policy = CreatePolicy();
        foreach (var collision in collisions)
        {
            var context = new ToolCatalogMergeContext(ToolCatalogMergeTestData.Request([first.Toolset, second.Toolset, toolset]), [first, second], [collision]);
            _ = (await policy.ResolveAsync(context, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolCatalogRejection>();
        }
    }

    /// <summary>Cancellation and invalid arguments never become policy rejection.</summary>
    [Fact]
    public async Task ResolveAsync_WhenCancelledOrNull_PropagatesExactFailure()
    {
        var policy = CreatePolicy();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new ToolCatalogMergeContext(ToolCaptureTestData.Discovery(), [], []);
        var cancelled = await Should.ThrowAsync<OperationCanceledException>(async () => await policy.ResolveAsync(context, cancellation.Token));
        cancelled.CancellationToken.ShouldBe(cancellation.Token);
        var invalid = await Should.ThrowAsync<ArgumentNullException>(async () => await policy.ResolveAsync(null!, cancellation.Token));
        invalid.GetType().ShouldBe(typeof(ArgumentNullException));
        invalid.ParamName.ShouldBe("context");
    }

    /// <summary>Concurrent requests cannot share mutable selection state.</summary>
    [Fact]
    public async Task ResolveAsync_WhenConcurrent_KeepsEachContributionGraphIndependent()
    {
        var policy = CreatePolicy();
        await Task.WhenAll(Enumerable.Range(0, 24).Select(async index =>
        {
            var candidate = ToolCatalogMergeTestData.Candidate(key: $"tools-{index}", id: $"tool-{index}");
            var context = new ToolCatalogMergeContext(ToolCatalogMergeTestData.Request([candidate.Toolset], $"principal-{index}"), [candidate], []);
            var selection = (await policy.ResolveAsync(context, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolCatalogSelection>();
            selection.Tools.ShouldBe([candidate]);
            selection.Aliases.Values.ShouldBe([candidate]);
        }));
    }
}
