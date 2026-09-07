// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

/// <summary>
/// Exercises candidate ordering, fallback policy, diagnostics, and the
/// determinism of model selection.
/// </summary>
public sealed class DefaultModelSelectorTests
{
    private readonly IModelSelector _selector = CreateSelector();

    [Fact]
    public async Task SelectAsync_WhenFirstCandidateMatches_SelectsIt()
    {
        var catalog = ProviderTestData.Catalog(
            ProviderTestData.Model("fast"),
            ProviderTestData.Model("smart"));
        var request = ProviderTestData.SelectionRequest(
            catalog,
            ProviderTestData.Policy(candidates: ["fast", "smart"]));

        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);

        var selected = result.ShouldBeOfType<ModelSelected>();
        selected.Decision.Model.Alias.Value.ShouldBe("fast");
        selected.Decision.CatalogVersion.ShouldBe(catalog.Version);
    }

    [Fact]
    public async Task SelectAsync_RecordsTheCatalogVersionTheChoiceWasMadeAgainst()
    {
        var catalog = new ModelCatalogSnapshot(
            new ModelCatalogVersion(7),
            [ProviderTestData.Model("fast")]);
        var request = ProviderTestData.SelectionRequest(
            catalog,
            ProviderTestData.Policy(candidates: ["fast"]));

        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ModelSelected>()
            .Decision.CatalogVersion.Value.ShouldBe(7);
    }

    [Fact]
    public async Task SelectAsync_WhenFirstCandidateMissingAndFallbackDisabled_ReturnsNoCompatibleModel()
    {
        var catalog = ProviderTestData.Catalog(ProviderTestData.Model("smart"));
        var request = ProviderTestData.SelectionRequest(
            catalog,
            ProviderTestData.Policy(candidates: ["fast", "smart"]));

        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);

        var none = result.ShouldBeOfType<NoCompatibleModel>();
        none.Diagnostics[0].Outcome.ShouldBe(ModelCandidateOutcome.NotInCatalog);
        none.Diagnostics[1].Outcome.ShouldBe(ModelCandidateOutcome.NotEvaluated);
    }

    [Fact]
    public async Task SelectAsync_WhenFirstCandidateMissingAndFallbackEnabled_SelectsNext()
    {
        var catalog = ProviderTestData.Catalog(ProviderTestData.Model("smart"));
        var request = ProviderTestData.SelectionRequest(
            catalog,
            ProviderTestData.Policy(
                ModelFallbackPolicy.OrderedCandidates,
                candidates: ["fast", "smart"]));

        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ModelSelected>().Decision.Model.Alias.Value.ShouldBe("smart");
    }

    [Fact]
    public async Task SelectAsync_WhenCandidateLacksRequiredCapability_SkipsItUnderFallback()
    {
        var catalog = ProviderTestData.Catalog(
            ProviderTestData.Model("no-tools", toolCalls: false),
            ProviderTestData.Model("tools", toolCalls: true));
        var request = ProviderTestData.SelectionRequest(
            catalog,
            ProviderTestData.Policy(
                ModelFallbackPolicy.OrderedCandidates,
                candidates: ["no-tools", "tools"]),
            new ModelRequirements { RequiresToolCalls = true });

        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);

        var selected = result.ShouldBeOfType<ModelSelected>();
        selected.Decision.Model.Alias.Value.ShouldBe("tools");
        selected.Decision.Diagnostics[0].Outcome
            .ShouldBe(ModelCandidateOutcome.MissingRequiredCapability);
    }

    [Fact]
    public async Task SelectAsync_WhenOnlyContextWindowIsTooSmall_ReportsInsufficientContextWindow()
    {
        var catalog = ProviderTestData.Catalog(
            ProviderTestData.Model("small", maxContextTokens: 100));
        var request = ProviderTestData.SelectionRequest(
            catalog,
            ProviderTestData.Policy(candidates: ["small"]),
            new ModelRequirements { MinimumInputTokens = 10_000 });

        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<NoCompatibleModel>()
            .Diagnostics.ShouldHaveSingleItem()
            .Outcome.ShouldBe(ModelCandidateOutcome.InsufficientContextWindow);
    }

    [Fact]
    public async Task SelectAsync_WhenNoCandidateMatches_ReportsEveryCandidate()
    {
        var catalog = ProviderTestData.Catalog();
        var request = ProviderTestData.SelectionRequest(
            catalog,
            ProviderTestData.Policy(
                ModelFallbackPolicy.OrderedCandidates,
                candidates: ["a", "b", "c"]));

        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<NoCompatibleModel>().Diagnostics.Length.ShouldBe(3);
    }

    [Fact]
    public async Task SelectAsync_WhenDowngradeAllowed_SelectsAndReportsAdjustmentCount()
    {
        var catalog = ProviderTestData.Catalog(
            ProviderTestData.Model("no-stream", streaming: false));
        var request = ProviderTestData.SelectionRequest(
            catalog,
            ProviderTestData.Policy(
                downgrade: CapabilityDowngradePolicy.AllowDeclaredAdjustments,
                candidates: ["no-stream"]),
            new ModelRequirements { RequiresStreaming = true });

        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);

        var selected = result.ShouldBeOfType<ModelSelected>();
        selected.Decision.Diagnostics.ShouldHaveSingleItem()
            .Reason.ShouldContain("adjustment");
    }

    [Fact]
    public async Task SelectAsync_MarksLaterCandidatesNotEvaluatedAfterASelection()
    {
        var catalog = ProviderTestData.Catalog(
            ProviderTestData.Model("a"),
            ProviderTestData.Model("b"));
        var request = ProviderTestData.SelectionRequest(
            catalog,
            ProviderTestData.Policy(
                ModelFallbackPolicy.OrderedCandidates,
                candidates: ["a", "b"]));

        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);

        var diagnostics = result.ShouldBeOfType<ModelSelected>().Decision.Diagnostics;
        diagnostics[0].Outcome.ShouldBe(ModelCandidateOutcome.Selected);
        diagnostics[1].Outcome.ShouldBe(ModelCandidateOutcome.NotEvaluated);
    }

    [Fact]
    public async Task SelectAsync_IsDeterministicForTheSameInputs()
    {
        var catalog = ProviderTestData.Catalog(
            ProviderTestData.Model("a"),
            ProviderTestData.Model("b"));
        var request = ProviderTestData.SelectionRequest(
            catalog,
            ProviderTestData.Policy(
                ModelFallbackPolicy.OrderedCandidates,
                candidates: ["a", "b"]));

        var first = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var second = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);

        first.ShouldBeOfType<ModelSelected>().Decision.Model.Alias
            .ShouldBe(second.ShouldBeOfType<ModelSelected>().Decision.Model.Alias);
    }

    [Fact]
    public async Task SelectAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await _selector.SelectAsync(
                null!,
                TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task SelectAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var request = ProviderTestData.SelectionRequest(
            ProviderTestData.Catalog(ProviderTestData.Model("a")),
            ProviderTestData.Policy(candidates: ["a"]));

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await _selector.SelectAsync(request, cancellation.Token));
    }

    private static IModelSelector CreateSelector()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentProviders();
        return services.BuildServiceProvider().GetRequiredService<IModelSelector>();
    }
}
