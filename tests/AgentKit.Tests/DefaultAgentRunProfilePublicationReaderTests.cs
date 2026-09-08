// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Diagnostics;
using System.Diagnostics.Metrics;

using AgentKit.Observability;

public sealed class DefaultAgentRunProfilePublicationReaderTests
{
    [Fact]
    public async Task ReadAsync_WhenCoordinatesMatch_ReturnsExactFrozenPublication()
    {
        var definition = CompositionTestData.Definition();
        var publication = CompositionTestData.RunProfile(definition);
        var source = new List<AgentRunProfilePublication> { publication };
        var reader = new DefaultAgentRunProfilePublicationReader(source);
        source.Clear();

        var result = await reader.ReadAsync(
            definition.Id, definition.Revision, TestContext.Current.CancellationToken);

        result.ShouldBe(new AgentRunProfilePublicationFound(publication));
        reader.CurrentSnapshot.Publications.ShouldBe([publication], ignoreOrder: false);
    }

    [Fact]
    public async Task ReadAsync_WhenRevisionDoesNotMatch_ReturnsTypedUnavailableWithoutFallback()
    {
        var definition = CompositionTestData.Definition();
        var reader = new DefaultAgentRunProfilePublicationReader(
            [CompositionTestData.RunProfile(definition)]);

        var result = await reader.ReadAsync(
            definition.Id,
            new AgentDefinitionRevision(definition.Revision.Value + 1),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AgentRunProfilePublicationUnavailable>();
    }

    [Fact]
    public async Task ReadAsync_WhenCancellationIsRequested_ThrowsBeforeLookup()
    {
        var definition = CompositionTestData.Definition();
        var reader = new DefaultAgentRunProfilePublicationReader(
            [CompositionTestData.RunProfile(definition)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await reader.ReadAsync(definition.Id, definition.Revision, cancellation.Token));
    }

    [Fact]
    public async Task ReadAsync_WhenArgumentsAreInvalid_ThrowsWithExactParameterNames()
    {
        var reader = new DefaultAgentRunProfilePublicationReader([]);

        var agent = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await reader.ReadAsync(default, new AgentDefinitionRevision(0)));
        agent.ParamName.ShouldBe("agentId");
    }

    [Fact]
    public async Task ReadAsync_WhenDiagnosticsThrow_PreservesFoundUnavailableAndCancellation()
    {
        using var parent = new Activity("run-profile-reader-hostile-meter-parent").Start();
        var parentTraceId = parent.TraceId;
        var definition = CompositionTestData.Definition();
        var publication = CompositionTestData.RunProfile(definition);
        var reader = new DefaultAgentRunProfilePublicationReader(
            [publication], new ThrowingRunProfilePublicationLogger());
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == AgentKitDiagnostics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (_, _, _, _) =>
            {
                if (Activity.Current?.TraceId == parentTraceId)
                {
                    throw new InvalidOperationException("Hostile meter.");
                }
            });
        listener.Start();

        var found = await reader.ReadAsync(
            definition.Id, definition.Revision, TestContext.Current.CancellationToken);
        var unavailable = await reader.ReadAsync(
            new AgentId(Guid.NewGuid()), definition.Revision, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await reader.ReadAsync(definition.Id, definition.Revision, cancellation.Token));

        _ = found.ShouldBeOfType<AgentRunProfilePublicationFound>();
        _ = unavailable.ShouldBeOfType<AgentRunProfilePublicationUnavailable>();
        Activity.Current.ShouldBe(parent);
    }

    [Fact]
    public void Constructor_WhenPublicationItemIsNull_ThrowsArgumentExceptionWithParameterName()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new DefaultAgentRunProfilePublicationReader([null!]));

        exception.ParamName.ShouldBe("publications");
    }
}
