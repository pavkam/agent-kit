// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddPatchTool_WhenCalledTwice_RegistersOneToolAndOneGenerator()
    {
        var services = CreateServices();

        _ = services.AddPatchTool();
        _ = services.AddPatchTool();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ITool>().Count(static tool => tool is PatchTool).ShouldBe(1);
        provider.GetServices<IIdentifierGenerator<WorkspaceMutationId>>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddPatchTool_WhenBoundIsInvalid_FailsOptionsValidation()
    {
        var services = CreateServices();
        _ = services.AddPatchTool(static options => options.MaximumEntries = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<PatchToolOptions>>().Value);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSnapshotReader, FakeSnapshotReader>();
        _ = services.AddSingleton<IWorkspacePatchApplier, FakePatchApplier>();
        _ = services.AddSingleton<ISecurityAuthority, SequencedSecurityAuthority>();
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>, SequenceSecurityRequestIdGenerator>();
        _ = services.AddSingleton<TimeProvider, FixedTimeProvider>();
        return services;
    }
}
