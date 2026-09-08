// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

/// <summary>Verifies explicit registration of the process-local grant-store leaf.</summary>
public sealed class ServiceExtensionsTests
{
    /// <summary>Verifies repeated leaf registration supplies one store and preserves ordinary DI replacement semantics.</summary>
    [Fact]
    public void AddInMemorySecurityGrantStore_WhenCalledRepeatedlyOrAfterHostStore_PreservesOneSelectedStore()
    {
        var replacement = new FixedGrantStore();
        var services = new ServiceCollection();
        _ = services.AddInMemorySecurityGrantStore();
        _ = services.AddInMemorySecurityGrantStore();

        using (var provider = services.BuildServiceProvider())
        {
            _ = provider.GetServices<ISecurityGrantStore>().ShouldHaveSingleItem()
                .ShouldBeOfType<InMemorySecurityGrantStore>();
        }

        services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(replacement);
        _ = services.AddInMemorySecurityGrantStore();
        using var replacementProvider = services.BuildServiceProvider();

        replacementProvider.GetRequiredService<ISecurityGrantStore>().ShouldBeSameAs(replacement);
        _ = replacementProvider.GetServices<ISecurityGrantStore>().ShouldHaveSingleItem();
    }

    /// <summary>Verifies a null service collection is rejected before any registration occurs.</summary>
    [Fact]
    public void AddInMemorySecurityGrantStore_WhenServicesIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => ServiceExtensions.AddInMemorySecurityGrantStore(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("services");
    }

    private sealed class FixedGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Unknown, 0, "The replacement store did not consume a grant."));

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }
}
