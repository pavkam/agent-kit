// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentArtifacts_WhenServicesIsNull_ThrowsExactArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddAgentArtifacts());

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAgentArtifacts_WhenRegistered_ProvidesDefaultCoordinatorAndGenerators()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IArtifactStore>(new RecordingArtifactStore());
        _ = services.AddSingleton<ISecurityAuthority>(new RecordingSecurityAuthority());
        _ = services.AddSingleton<TimeProvider>(new FixedTimeProvider());
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>>(
            new FixedIdentifierGenerator<SecurityRequestId>(ArtifactTestData.SecurityRequestId));

        _ = services.AddAgentArtifacts(static options => options.ProfileKey = new ArtifactProfileKey("test"));
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IArtifactCoordinator>().ShouldBeOfType<DefaultArtifactCoordinator>();
        _ = provider.GetRequiredService<IProcessOutputArtifactSink>().ShouldBeOfType<ArtifactProcessOutputSink>();
        provider.GetRequiredService<IIdentifierGenerator<ArtifactId>>().Create().Value.ShouldNotBe(Guid.Empty);
        provider.GetRequiredService<IIdentifierGenerator<ArtifactPreparationId>>().Create().Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void AddAgentArtifacts_WhenMechanicsAreInvalid_FailsOptionsValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentArtifacts(static options => options.MaximumArtifactBytes = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AgentArtifactOptions>>().Value);
    }

    [Fact]
    public void AddAgentArtifacts_WhenAlreadyRegistered_DoesNotReplaceExistingCoordinator()
    {
        var services = new ServiceCollection();
        var existing = new RecordingArtifactCoordinator();
        _ = services.AddSingleton<IArtifactCoordinator>(existing);

        _ = services.AddAgentArtifacts();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IArtifactCoordinator>().ShouldBeSameAs(existing);
    }
}
