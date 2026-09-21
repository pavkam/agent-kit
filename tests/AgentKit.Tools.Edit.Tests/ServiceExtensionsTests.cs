// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit.Tests;

using AgentKit.TestSupport;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddEditTool_WhenCalledTwice_RegistersOneToolAndOneMutationGenerator()
    {
        var services = CreateServices();
        _ = services.AddEditTool();
        _ = services.AddEditTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<EditTool>();
        _ = provider.GetRequiredService<IIdentifierGenerator<WorkspaceMutationId>>();
    }

    [Fact]
    public void AddEditTool_WhenBoundsInvalid_FailsOptionsValidation()
    {
        var services = CreateServices();
        _ = services.AddEditTool(static options => options.DefaultMaximumBytes = options.MaximumBytes + 1);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EditToolOptions>>().Value);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSnapshotReader, FakeSnapshotReader>();
        _ = services.AddSingleton<IAtomicFileReplacer, FakeAtomicFileReplacer>();
        _ = services.AddSingleton<ISecurityAuthority, SequencedSecurityAuthority>();
        _ = services.AddSingleton<ISecurityAuthoritySelector>(sp => new FixedSecurityAuthoritySelector(sp.GetRequiredService<ISecurityAuthority>()));
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>, SequenceSecurityRequestIdGenerator>();
        _ = services.AddSingleton<TimeProvider, FixedTimeProvider>();
        return services;
    }
}
