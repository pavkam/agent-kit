// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Defines exact revision, no-fallback, cancellation, and concurrent lookup requirements for projection-policy catalogs.</summary>
public abstract class ToolResultProjectionPolicyCatalogConformanceTests
{
    /// <summary>Composes an isolated implementation with the supplied retained revisions.</summary>
    /// <param name="policies">The initialized immutable policy set for this fixture.</param>
    /// <returns>A fixture owning the catalog and any resources created during composition.</returns>
    protected abstract ToolResultProjectionPolicyCatalogFixture CreateFixture(ImmutableArray<ToolResultProjectionPolicySnapshot> policies);

    /// <summary>Verifies that both an older and a newer retained revision resolve exactly.</summary>
    [Fact]
    public async Task ResolveAsync_WhenSeveralRevisionsExist_ReturnsTheRequestedContent()
    {
        // Arrange
        var older = ToolProjectionPolicyTestData.Snapshot(version: 1, maximumBytes: 32);
        var newer = ToolProjectionPolicyTestData.Snapshot(version: 2, maximumBytes: 64);
        await using var fixture = CreateFixture([newer, older]);

        // Act / Assert
        foreach (var expected in new[] { older, newer })
        {
            var result = await fixture.Catalog.ResolveAsync(expected.Reference, TestContext.Current.CancellationToken);
            var resolved = result.ShouldBeOfType<ToolResultProjectionPolicyResolved>();
            resolved.Reference.ShouldBe(expected.Reference);
            resolved.Snapshot.ShouldBe(expected);
        }
    }

    /// <summary>Verifies that an absent revision never selects the latest retained content.</summary>
    [Fact]
    public async Task ResolveAsync_WhenRevisionIsUnavailable_DoesNotFallBack()
    {
        // Arrange
        var present = ToolProjectionPolicyTestData.Snapshot(version: 2);
        var requested = ToolProjectionPolicyTestData.Snapshot(version: 1).Reference;
        await using var fixture = CreateFixture([present]);

        // Act
        var result = await fixture.Catalog.ResolveAsync(requested, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeOfType<ToolResultProjectionPolicyUnavailable>().Reference.ShouldBe(requested);
    }

    /// <summary>Verifies ordinal, case-sensitive policy identity without aliases or fallback keys.</summary>
    [Theory]
    [InlineData("Projection.history")]
    [InlineData("projection.other")]
    public async Task ResolveAsync_WhenKeyDiffers_ReturnsUnavailable(string requestedKey)
    {
        // Arrange
        await using var fixture = CreateFixture([ToolProjectionPolicyTestData.Snapshot()]);
        var requested = ToolProjectionPolicyTestData.Snapshot(key: requestedKey).Reference;

        // Act
        var result = await fixture.Catalog.ResolveAsync(requested, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeOfType<ToolResultProjectionPolicyUnavailable>().Reference.ShouldBe(requested);
    }

    /// <summary>Verifies that an empty explicit catalog does not invent a default policy.</summary>
    [Fact]
    public async Task ResolveAsync_WhenCatalogIsEmpty_PreservesTheUnavailableReference()
    {
        // Arrange
        await using var fixture = CreateFixture([]);
        var requested = ToolProjectionPolicyTestData.Snapshot().Reference;

        // Act
        var result = await fixture.Catalog.ResolveAsync(requested, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeOfType<ToolResultProjectionPolicyUnavailable>().Reference.ShouldBe(requested);
    }

    /// <summary>Verifies cancellation remains cancellation for both available and unavailable references.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResolveAsync_WhenCallerIsCancelled_PropagatesTheExactToken(bool available)
    {
        // Arrange
        var policy = ToolProjectionPolicyTestData.Snapshot();
        await using var fixture = CreateFixture(available ? [policy] : []);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await fixture.Catalog.ResolveAsync(policy.Reference, cancellation.Token));

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    /// <summary>Verifies null is rejected through the typed public boundary.</summary>
    [Fact]
    public async Task ResolveAsync_WhenReferenceIsNull_RejectsTheExactParameter()
    {
        // Arrange
        await using var fixture = CreateFixture([]);

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await fixture.Catalog.ResolveAsync(null!, TestContext.Current.CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("reference");
    }

    /// <summary>Verifies concurrent readers and retries preserve independent exact revisions.</summary>
    [Fact]
    public async Task ResolveAsync_WhenReadersRunConcurrently_RetainsExactImmutableContent()
    {
        // Arrange
        var first = ToolProjectionPolicyTestData.Snapshot(version: 1, maximumBytes: 64);
        var second = ToolProjectionPolicyTestData.Snapshot(version: 2, maximumBytes: 128);
        var missing = ToolProjectionPolicyTestData.Snapshot(version: 3).Reference;
        await using var fixture = CreateFixture([first, second]);

        // Act / Assert
        await Task.WhenAll(Enumerable.Range(0, 48).Select(index => Task.Run(async () =>
        {
            if (index % 3 == 2)
            {
                var result = await fixture.Catalog.ResolveAsync(missing, TestContext.Current.CancellationToken);
                result.ShouldBeOfType<ToolResultProjectionPolicyUnavailable>().Reference.ShouldBe(missing);
                return;
            }

            var expected = index % 3 == 0 ? first : second;
            var resolved = (await fixture.Catalog.ResolveAsync(expected.Reference, TestContext.Current.CancellationToken))
                .ShouldBeOfType<ToolResultProjectionPolicyResolved>();
            resolved.Snapshot.ShouldBe(expected);
        }, TestContext.Current.CancellationToken)));
    }
}
