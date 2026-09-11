// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.Conformance;

internal sealed class RegisteredProjectionPolicyCatalogFixture: ToolResultProjectionPolicyCatalogFixture
{
    private readonly ServiceProvider _provider;

    public RegisteredProjectionPolicyCatalogFixture(ImmutableArray<ToolResultProjectionPolicySnapshot> policies)
    {
        var services = new ServiceCollection();
        _ = services.AddToolResultProjectionPolicyCatalog();
        foreach (var policy in policies)
        {
            _ = services.AddToolResultProjectionPolicy(policy);
        }

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Catalog = _provider.GetRequiredService<IToolResultProjectionPolicyCatalog>();
    }

    public override IToolResultProjectionPolicyCatalog Catalog { get; }

    public override ValueTask DisposeAsync() => _provider.DisposeAsync();
}
