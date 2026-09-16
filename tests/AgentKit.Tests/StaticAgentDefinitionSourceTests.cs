// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Verifies StaticAgentDefinitionSource behavior and contracts.</summary>
public sealed class StaticAgentDefinitionSourceTests
{
    [Fact]
    public async Task ReadAsync_WhenCalled_AlwaysReturnsTheSameFixedSnapshot()
    {
        var definition = CompositionTestData.Definition();
        var source = new StaticAgentDefinitionSource(new AgentDefinitionSourceId("static"), [definition], precedence: 3);

        var first = await source.ReadAsync(TestContext.Current.CancellationToken);
        var second = await source.ReadAsync(TestContext.Current.CancellationToken);

        first.ShouldBeSameAs(second);
        first.SourceId.ShouldBe(source.SourceId);
        first.Precedence.ShouldBe(3);
        first.Version.ShouldBe(new AgentDefinitionSourceVersion(0));
        first.Definitions.ShouldBe([definition]);
    }

    [Fact]
    public async Task ReadAsync_WhenCancelled_ThrowsBeforeReturningTheSnapshot()
    {
        var source = new StaticAgentDefinitionSource(new AgentDefinitionSourceId("static"), []);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await source.ReadAsync(cancellation.Token));
    }
}
