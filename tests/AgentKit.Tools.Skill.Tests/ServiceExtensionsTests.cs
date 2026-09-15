// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddSkillTool_WhenCalledTwice_AddsOneToolDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddSkillTool().AddSkillTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(SkillTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IToolPresentationFormatter)
            && descriptor.ImplementationType == typeof(SkillToolPresentationFormatter)).ShouldBe(1);
    }

    [Fact]
    public void AddSkillTool_WhenResolved_SharesOneCatalogBetweenToolAndContextInventory()
    {
        var services = new ServiceCollection();
        _ = services.AddSkillTool(options => options.Skills.Add(Definition()));
        _ = services.AddSingleton<IFileSnapshotReader, RecordingSnapshotReader>();
        _ = services.AddSingleton<ISecurityAuthority, RecordingSecurityAuthority>();
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>, FixedSecurityRequestIdGenerator>();
        _ = services.AddSingleton<TimeProvider, FixedTimeProvider>();
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<ISkillCatalog>();
        var context = provider.GetRequiredService<ISkillCatalogContextSource>();
        ReferenceEquals(catalog, context).ShouldBeTrue();
        context.CatalogVersion.ShouldBe(catalog.Snapshot.Version);
    }

    private static SkillDefinition Definition(ContentHash? expectedHash = null) => new(new SkillId("docs"), "Documentation", "Project documentation.", SkillTrust.Workspace, new FileSystemPath("private/source.md"), expectedHash);
}
