// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry.Tests;

/// <summary>Verifies option validation and snapshot capture performed at the composition boundary.</summary>
public sealed class OpenTelemetryObservationRegistrationTests
{
    [Fact]
    public void Add_WhenServicesAreNull_ThrowsArgumentNullExceptionNamingServices()
    {
        var exception = Should.Throw<ArgumentNullException>(() => OpenTelemetryObservationRegistration.Add(null!, Key(), static _ => { }));

        exception.ParamName.ShouldBe("services");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-3L)]
    public void Add_WhenExporterVersionIsNotPositive_ThrowsArgumentOutOfRangeException(long version)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Register(options => options.ExporterVersion = version));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Add_WhenNoSignalIsEnabled_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Register(static options => options.Signals = OpenTelemetrySignalSet.None)).ParamName.ShouldBe("options");

    [Fact]
    public void Add_WhenSignalFlagIsUndefined_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Register(static options => options.Signals = (OpenTelemetrySignalSet) 1024)).ParamName.ShouldBe("options");

    [Fact]
    public void Add_WhenContentClassificationIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Register(static options => options.ContentClassification = (DataClassification) 99));

    [Fact]
    public void Add_WhenAnOptionObjectIsNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() => Register(static options => options.Delivery = null!));
        _ = Should.Throw<ArgumentNullException>(() => Register(static options => options.Bounds = null!));
        _ = Should.Throw<ArgumentNullException>(() => Register(static options => options.ContentCapture = null!));
        _ = Should.Throw<ArgumentNullException>(() => Register(static options => options.AllowedClassifications = null!));
    }

    [Fact]
    public void Add_WhenCaptureIsEnabledButTheClassificationIsNotAllowed_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Register(static options =>
        {
            options.ContentCapture = new ObservationContentCapturePolicy(true);
            options.ContentClassification = DataClassification.Restricted;
        })).ParamName.ShouldBe("options");

    [Fact]
    public void Add_WhenCaptureIsEnabledWithoutTheActivitiesSignal_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Register(static options =>
        {
            options.ContentCapture = new ObservationContentCapturePolicy(true);
            options.ContentClassification = DataClassification.Public;
            options.Signals = OpenTelemetrySignalSet.Metrics;
        })).ParamName.ShouldBe("options");

    [Fact]
    public void Add_WhenRequiredAuditDeliveryHasNoDurableExporter_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Register(static options =>
        {
            options.Signals = OpenTelemetrySignalSet.Audit;
            options.Delivery = new ObservationDeliveryPolicy(true, TimeSpan.FromSeconds(1));
        })).ParamName.ShouldBe("options");

    [Fact]
    public void Add_WhenRequiredAuditDeliveryHasADurableExporter_Registers() =>
        Should.NotThrow(() => Register(static options =>
        {
            options.Signals = OpenTelemetrySignalSet.Audit;
            options.Delivery = new ObservationDeliveryPolicy(true, TimeSpan.FromSeconds(1));
            options.AuditExporterProvidesDurableAcceptance = true;
        }));

    [Fact]
    public void Add_WhenOptionsAreMutatedAfterRegistration_ExistingSnapshotIsUnaffected()
    {
        var services = new ServiceCollection();
        var key = Key();
        OpenTelemetryObservationOptions? captured = null;
        _ = OpenTelemetryObservationRegistration.Add(services, key, options => captured = options);

        captured!.ExporterVersion = 9;

        var declaration = services.Select(static descriptor => descriptor.ImplementationInstance).OfType<OpenTelemetryObservationDeclaration>().Single();
        declaration.Snapshot.ExporterVersion.Value.ShouldBe(1);
    }

    [Fact]
    public void Add_WhenTheSignalsRequireIt_RegistersTheRunEventSinkWithTheDeclaredDeliveryAndFlushDeadline()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenTelemetryObservability(Key(), static options =>
        {
            options.Delivery = new ObservationDeliveryPolicy(true, TimeSpan.FromSeconds(5));
        });

        services.Count(static descriptor => descriptor.ServiceType == typeof(IRunEventSink)).ShouldBe(1);
    }

    private static ObservationExporterKey Key() => new($"test-{Guid.NewGuid():N}");

    private static IServiceCollection Register(Action<OpenTelemetryObservationOptions> configure) =>
        OpenTelemetryObservationRegistration.Add(new ServiceCollection(), Key(), configure);
}
