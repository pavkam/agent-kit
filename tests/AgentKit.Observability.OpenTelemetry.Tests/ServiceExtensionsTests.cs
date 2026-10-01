// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry.Tests;

using AgentKit.Permissions;

/// <summary>Verifies the public OpenTelemetry observation registration surface.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddOpenTelemetryObservability_WhenServicesAreNull_ThrowsArgumentNullExceptionNamingServices()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddOpenTelemetryObservability(Key(), static _ => { }));

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenConfigureIsNull_ThrowsArgumentNullExceptionNamingConfigure()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddOpenTelemetryObservability(Key(), null!));

        exception.ParamName.ShouldBe("configure");
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenKeyIsDefault_ThrowsArgumentExceptionNamingKey()
    {
        var exception = Should.Throw<ArgumentException>(() => new ServiceCollection().AddOpenTelemetryObservability(default, static _ => { }));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenNoRedactorIsRegistered_AddsTheOmissionOnlyRedactor()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenTelemetryObservability(Key(), static _ => { });
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IObservationRedactor>().ShouldBeOfType<OmissionOnlyObservationRedactor>();
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenRedactorIsAlreadyRegistered_KeepsTheReplacement()
    {
        var services = new ServiceCollection();
        var redactor = new CallbackObservationRedactor(static (_, _) => new ContentOmitted());
        _ = services.AddSingleton<IObservationRedactor>(redactor);
        _ = services.AddOpenTelemetryObservability(Key(), static _ => { });
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IObservationRedactor>().ShouldBeSameAs(redactor);
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenRegisteredTwiceWithIdenticalOptions_IsIdempotent()
    {
        var services = new ServiceCollection();
        var key = Key();
        _ = services.AddOpenTelemetryObservability(key, static _ => { });
        var count = services.Count;

        _ = services.AddOpenTelemetryObservability(key, static _ => { });

        services.Count.ShouldBe(count);
        using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IRunEventSink>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenKeyIsReusedWithDifferentOptions_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var key = Key();
        _ = services.AddOpenTelemetryObservability(key, static _ => { });

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddOpenTelemetryObservability(key, static options => options.ExporterVersion = 2));

        exception.Message.ShouldContain(key.Value);
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenTwoKeysAreRegistered_CreatesOneIndependentRunEventSinkPerKey()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenTelemetryObservability(Key(), static _ => { });
        _ = services.AddOpenTelemetryObservability(Key(), static options => options.Signals = OpenTelemetrySignalSet.Logs);
        using var provider = services.BuildServiceProvider();

        provider.GetServices<IRunEventSink>().Count().ShouldBe(2);
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenOnlyAuditIsEnabled_RegistersNoRunEventSink()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IOpenTelemetryAuditExporter>(new RecordingAuditExporter());
        _ = services.AddOpenTelemetryObservability(Key(), static options => options.Signals = OpenTelemetrySignalSet.Audit);
        using var provider = services.BuildServiceProvider();

        provider.GetServices<IRunEventSink>().ShouldBeEmpty();
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenContentCaptureIsEnabledWithOnlyTheOmissionRedactor_FailsWhenTheSinkIsResolved()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenTelemetryObservability(Key(), static options =>
        {
            options.ContentCapture = new ObservationContentCapturePolicy(true);
            options.ContentClassification = DataClassification.Internal;
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<InvalidOperationException>(() => provider.GetServices<IRunEventSink>().ToArray());
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenTheAuditSignalHasNoExporter_FailsWhenTheSinkIsResolved()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentPermissions(static options => options.PolicySnapshot = TestSecurityEvidence.PolicySnapshot);
        _ = services.AddOpenTelemetryObservability(Key(), static options => options.Signals = OpenTelemetrySignalSet.Audit);
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ISecurityAuditDispatcher>);

        exception.Message.ShouldContain("IOpenTelemetryAuditExporter");
    }

    private static ObservationExporterKey Key() => new($"test-{Guid.NewGuid():N}");
}
