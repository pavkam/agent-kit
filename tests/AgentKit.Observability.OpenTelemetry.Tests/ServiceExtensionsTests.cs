// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry.Tests;

using AgentKit.Observability.OpenTelemetry;

/// <summary>Verifies OpenTelemetry observation registration behavior.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddOpenTelemetryObservability_WhenDuplicateKeyRegistered_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var key = new ObservationExporterKey("test");
        _ = services.AddOpenTelemetryObservability(key, static _ => { });
        _ = Should.Throw<InvalidOperationException>(() => services.AddOpenTelemetryObservability(key, static _ => { }));
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenContentCaptureDisabledByDefault_RegistersOmissionRedactor()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenTelemetryObservability(new ObservationExporterKey("enabled-default"), static _ => { });
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IObservationRedactor>().ShouldBeOfType<OmissionOnlyObservationRedactor>();
    }
}
