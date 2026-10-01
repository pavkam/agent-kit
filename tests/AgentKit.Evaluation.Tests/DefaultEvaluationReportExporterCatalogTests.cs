// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class DefaultEvaluationReportExporterCatalogTests
{
    [Fact]
    public void Constructor_WhenServicesAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DefaultEvaluationReportExporterCatalog(null!)).ParamName.ShouldBe("services");

    [Fact]
    public void Find_WhenKeyIsBlank_ThrowsArgumentException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Should.Throw<ArgumentException>(() => new DefaultEvaluationReportExporterCatalog(provider).Find(default)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void Find_WhenExporterIsRegistered_ReturnsItAndOtherwiseReturnsNull()
    {
        var exporter = new RecordingReportExporter();
        using var provider = new ServiceCollection().AddKeyedSingleton<IEvaluationReportExporter>("known", exporter).BuildServiceProvider();
        var catalog = new DefaultEvaluationReportExporterCatalog(provider);

        catalog.Find(new EvaluationReportExporterKey("known")).ShouldBeSameAs(exporter);
        catalog.Find(new EvaluationReportExporterKey("absent")).ShouldBeNull();
    }
}
