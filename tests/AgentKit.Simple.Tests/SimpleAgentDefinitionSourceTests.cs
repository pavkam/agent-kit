// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

/// <summary>Verifies SimpleAgentDefinitionSource behavior and contracts.</summary>
public sealed class SimpleAgentDefinitionSourceTests
{
    [Fact]
    public async Task ReadAsync_WhenCalled_ReturnsTheSnapshotComputedAtConstruction()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };
        var source = new SimpleAgentDefinitionSource(plan, []);

        var snapshot = await source.ReadAsync(TestContext.Current.CancellationToken);

        snapshot.ShouldBeSameAs(source.Snapshot);
        snapshot.SourceId.ShouldBe(source.SourceId);
        _ = snapshot.Definitions.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ReadAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };
        var source = new SimpleAgentDefinitionSource(plan, []);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await source.ReadAsync(cts.Token));
    }

    [Fact]
    public void Constructor_WhenPlanIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SimpleAgentDefinitionSource(null!, []));

        exception.ParamName.ShouldBe("plan");
    }

    [Fact]
    public void Constructor_WhenToolsIsNull_ThrowsArgumentNullException()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };

        var exception = Should.Throw<ArgumentNullException>(() => new SimpleAgentDefinitionSource(plan, null!));

        exception.ParamName.ShouldBe("tools");
    }

    [Fact]
    public void Constructor_WhenPlanIsIncomplete_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() => new SimpleAgentDefinitionSource(new SimpleAgentPlan(), []));
}
