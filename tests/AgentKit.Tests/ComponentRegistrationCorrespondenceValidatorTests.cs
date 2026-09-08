// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class ComponentRegistrationCorrespondenceValidatorTests
{
    [Fact]
    public void Validate_WhenSnapshotIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            ComponentRegistrationCorrespondenceValidator.Validate(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("snapshot");
    }

    [Fact]
    public void MatchesImplementation_WhenServiceIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            ComponentRegistrationCorrespondenceValidator.MatchesImplementation(null!, typeof(Leaf)));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("service");
    }

    [Fact]
    public void MatchesImplementation_WhenImplementationTypeIsNull_ThrowsExactArgumentNullException()
    {
        var service = ServiceDescriptor.Singleton<ILeaf, Leaf>();

        var exception = Should.Throw<ArgumentNullException>(() =>
            ComponentRegistrationCorrespondenceValidator.MatchesImplementation(service, null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("implementationType");
    }

    [Fact]
    public void Validate_WhenDirectKeyedAndInstanceRegistrationsMatch_ReturnsNoDiagnostics()
    {
        var instance = new InstanceLeaf();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ILeaf, Leaf>();
        _ = services.AddKeyedScoped<IKeyedLeaf, KeyedLeaf>("alpha");
        _ = services.AddSingleton<IInstanceLeaf>(instance);
        _ = services.DeclareAgentKitComponent(Registration<ILeaf, Leaf>(ServiceLifetime.Singleton));
        _ = services.DeclareAgentKitComponent(Registration<IKeyedLeaf, KeyedLeaf>(ServiceLifetime.Scoped, "alpha"));
        _ = services.DeclareAgentKitComponent(Registration<IInstanceLeaf, InstanceLeaf>(ServiceLifetime.Singleton));

        var diagnostics = ComponentRegistrationCorrespondenceValidator.Validate(
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenOpaqueFactoryIsExplicitlyDeclared_DoesNotInvokeIt()
    {
        var factoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddSingleton<ILeaf>(
            _ =>
            {
                factoryCalls++;
                return new Leaf();
            });
        _ = services.DeclareAgentKitComponent(Registration<ILeaf, Leaf>(ServiceLifetime.Singleton));

        var diagnostics = ComponentRegistrationCorrespondenceValidator.Validate(
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.ShouldBeEmpty();
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Validate_WhenServiceIsMissing_ReportsMissingCorrespondence()
    {
        var services = new ServiceCollection();
        _ = services.DeclareAgentKitComponent(Registration<ILeaf, Leaf>(ServiceLifetime.Singleton));

        var diagnostics = ComponentRegistrationCorrespondenceValidator.Validate(
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-registration.service-missing");
    }

    [Fact]
    public void Validate_WhenServiceKeyDiffers_ReportsMissingExactCorrespondence()
    {
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<IKeyedLeaf, KeyedLeaf>("alpha");
        _ = services.DeclareAgentKitComponent(
            Registration<IKeyedLeaf, KeyedLeaf>(ServiceLifetime.Singleton, "beta"));

        var diagnostics = ComponentRegistrationCorrespondenceValidator.Validate(
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-registration.service-missing");
    }

    [Fact]
    public void Validate_WhenLifetimeOrObservableImplementationDiffers_ReportsBothMismatches()
    {
        var services = new ServiceCollection();
        _ = services.AddScoped<ILeaf, Leaf>();
        _ = services.AddSingleton<ISecondLeaf, OtherSecondLeaf>();
        _ = services.DeclareAgentKitComponent(Registration<ILeaf, Leaf>(ServiceLifetime.Singleton));
        _ = services.DeclareAgentKitComponent(Registration<ISecondLeaf, SecondLeaf>(ServiceLifetime.Singleton));

        var diagnostics = ComponentRegistrationCorrespondenceValidator.Validate(
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-registration.lifetime-mismatch");
        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-registration.implementation-mismatch");
    }

    [Fact]
    public void Validate_WhenDescriptorOrActualServiceRepeats_ReportsBothDuplicates()
    {
        var registration = Registration<ILeaf, Leaf>(ServiceLifetime.Singleton);
        var services = new ServiceCollection();
        _ = services.AddSingleton<ILeaf, Leaf>();
        _ = services.AddSingleton<ILeaf, Leaf>();
        _ = services.DeclareAgentKitComponent(registration);
        _ = services.DeclareAgentKitComponent(registration);

        var diagnostics = ComponentRegistrationCorrespondenceValidator.Validate(
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-registration.descriptor-duplicate");
        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-registration.service-duplicate");
    }

    [Fact]
    public void Validate_WhenClaimedAddressHasExtraActualRegistration_ReportsUndeclaredService()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ILeaf, Leaf>();
        _ = services.AddSingleton<ILeaf, OtherLeaf>();
        _ = services.DeclareAgentKitComponent(Registration<ILeaf, Leaf>(ServiceLifetime.Singleton));

        var diagnostics = ComponentRegistrationCorrespondenceValidator.Validate(
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-registration.service-undeclared");
    }

    [Fact]
    public void Validate_WhenMetadataUsesFactory_RejectsWithoutExecutingFactory()
    {
        var factoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddSingleton(
            _ =>
            {
                factoryCalls++;
                return Registration<ILeaf, Leaf>(ServiceLifetime.Singleton);
            });

        var diagnostics = ComponentRegistrationCorrespondenceValidator.Validate(
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-registration.metadata-opaque");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Validate_WhenMetadataIsKeyed_RejectsWithoutReadingItAsADeclaration()
    {
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton("metadata", Registration<ILeaf, Leaf>(ServiceLifetime.Singleton));

        var diagnostics = ComponentRegistrationCorrespondenceValidator.Validate(
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-registration.metadata-opaque");
    }

    private static ComponentRegistrationDescriptor Registration<TContract, TImplementation>(
        ServiceLifetime lifetime,
        string? key = null)
        where TContract : class
        where TImplementation : class, TContract => new(
            new ComponentContractReference(typeof(TContract), key),
            typeof(TImplementation),
            lifetime,
            []);

    private interface ILeaf;
    private interface ISecondLeaf;
    private interface IKeyedLeaf;
    private interface IInstanceLeaf;

    private sealed class Leaf: ILeaf;
    private sealed class OtherLeaf: ILeaf;
    private sealed class SecondLeaf: ISecondLeaf;
    private sealed class OtherSecondLeaf: ISecondLeaf;
    private sealed class KeyedLeaf: IKeyedLeaf;
    private sealed class InstanceLeaf: IInstanceLeaf;
}
