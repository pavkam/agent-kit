// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

public sealed class BudgetProfileRegistrationTests
{
    [Fact]
    public void AddBudgetProfile_WhenRegistered_ResolvesThroughCatalog()
    {
        var services = new ServiceCollection();
        var profileKey = new BudgetProfileKey("standard");
        _ = services.AddBudgetProfile(profileKey, options =>
        {
            options.Version = new BudgetProfileVersion(3);
            options.Limits.Add(new BudgetLimit(BudgetDimensions.Turns, 5, new BudgetUnit("count"), BudgetLimitKind.Hard));
        });
        _ = services.AddSingleton<IBudgetLedger, RecordingBudgetLedger>();

        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IBudgetProfileCatalog>();
        catalog.TryGet(profileKey, out var profile).ShouldBeTrue();
        profile!.Version.ShouldBe(new BudgetProfileVersion(3));
        profile.Limits.Single().Dimension.ShouldBe(BudgetDimensions.Turns);
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenProfileMissing_ReturnsProfileNotFound()
    {
        var ledger = new RecordingBudgetLedger
        {
            CreateResult = new BudgetLedgerScopeCreated(new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address()))
        };
        var authority = TestFactory.Authority(ledger);
        var request = new BudgetScopeRequest(
            null,
            TestFactory.Address(),
            new BudgetProfileKey("missing"),
            [],
            new IdempotencyKey("profile-missing"));

        var result = await authority.CreateChildScopeAsync(request, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<BudgetScopeCreationFailed>();
        failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.ProfileNotFound);
        ledger.CreateRequest.ShouldBeNull();
    }
}
