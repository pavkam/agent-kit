// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

/// <summary>Verifies explicit registration of the process-local grant-store leaf.</summary>
public sealed class ServiceExtensionsTests
{
    /// <summary>Verifies repeated leaf registration is idempotent while a different store remains visibly ambiguous.</summary>
    [Fact]
    public void AddInMemorySecurityGrantStore_WhenCalledRepeatedlyOrAfterHostStore_PreservesEveryDistinctSelection()
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

        var selections = replacementProvider.GetServices<ISecurityGrantStore>().ToArray();
        selections.Length.ShouldBe(2);
        selections.ShouldContain(replacement);
        selections.ShouldContain(static store => store is InMemorySecurityGrantStore);
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

    /// <summary>Verifies same-concrete instance and different-lifetime registrations cannot suppress the leaf singleton.</summary>
    [Fact]
    public void AddInMemorySecurityGrantStore_WhenSameConcreteAlternativeExists_PreservesVisibleAmbiguity()
    {
        var existing = new InMemorySecurityGrantStore(TimeProvider.System);
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(existing);
        _ = services.AddScoped<ISecurityGrantStore, InMemorySecurityGrantStore>();

        _ = services.AddInMemorySecurityGrantStore();

        var descriptors = services.Where(static descriptor => descriptor.ServiceType == typeof(ISecurityGrantStore)).ToArray();
        descriptors.Length.ShouldBe(3);
        descriptors.ShouldContain(static descriptor => descriptor.Lifetime == ServiceLifetime.Singleton
            && (descriptor.ImplementationType == typeof(InMemorySecurityGrantStore)
                || descriptor.ImplementationFactory != null));
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

        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevocationNotFound(grantId));
        }
    }
}
