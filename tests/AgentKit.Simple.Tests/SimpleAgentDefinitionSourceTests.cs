// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

using AgentKit.Tools;

using Microsoft.Extensions.Options;

/// <summary>Verifies SimpleAgentDefinitionSource behavior and contracts.</summary>
public sealed class SimpleAgentDefinitionSourceTests
{
    [Fact]
    public async Task ReadAsync_WhenCalled_ReturnsTheSnapshotComputedAtConstruction()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };
        var source = new SimpleAgentDefinitionSource(plan, [], AllowAll);

        var snapshot = await source.ReadAsync(TestContext.Current.CancellationToken);

        snapshot.ShouldBeSameAs(source.Snapshot);
        snapshot.SourceId.ShouldBe(source.SourceId);
        _ = snapshot.Definitions.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ReadAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };
        var source = new SimpleAgentDefinitionSource(plan, [], AllowAll);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await source.ReadAsync(cts.Token));
    }

    [Fact]
    public void Constructor_WhenPlanIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SimpleAgentDefinitionSource(null!, [], AllowAll));

        exception.ParamName.ShouldBe("plan");
    }

    [Fact]
    public void Constructor_WhenToolsIsNull_ThrowsArgumentNullException()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };

        var exception = Should.Throw<ArgumentNullException>(() => new SimpleAgentDefinitionSource(plan, null!, AllowAll));

        exception.ParamName.ShouldBe("tools");
    }

    [Fact]
    public void Constructor_WhenToolOptionsIsNull_ThrowsArgumentNullException()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };

        var exception = Should.Throw<ArgumentNullException>(() => new SimpleAgentDefinitionSource(plan, [], null!));

        exception.ParamName.ShouldBe("toolOptions");
    }

    [Fact]
    public void Constructor_WhenAdditionalAgentsArePlanned_PublishesEveryDefinition()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };
        var other = new AgentId(Guid.NewGuid());
        plan.AddAgent(other, new SimpleAgentOptions { DisplayName = "other", MaxTurns = 2 });

        var source = new SimpleAgentDefinitionSource(plan, [], AllowAll);

        source.Snapshot.Definitions.Length.ShouldBe(2);
        var published = source.Snapshot.Definitions.Single(d => d.Id == other);
        published.DisplayName.ShouldBe("other");
        published.RunDefaults.MaxTurns.ShouldBe(2);
        published.SecurityProfile.ShouldBe(plan.SecurityProfileKey);
        published.SessionProfile.ShouldBe(plan.SessionProfileKey);
    }

    [Fact]
    public void Constructor_WhenPlanIsIncomplete_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() => new SimpleAgentDefinitionSource(new SimpleAgentPlan(), [], AllowAll));

    private static IOptions<AgentToolsOptions> AllowAll { get; } =
        Options.Create(new AgentToolsOptions { AllowAllRegisteredTools = true });
}
