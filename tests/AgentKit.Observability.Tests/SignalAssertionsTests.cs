// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.Tests;

using System.Collections.Frozen;
using System.Collections.Immutable;

/// <summary>Verifies the shared protected-content assertion over activities, logs, and metrics.</summary>
public sealed class SignalAssertionsTests
{
    [Fact]
    public void ShouldNotContainContent_WhenNoSignalContainsTheLiteral_Passes() =>
        Should.NotThrow(() => SignalAssertions.ShouldNotContainContent([Activity("op", "tag", "safe")], [Log("safe", "k", "v")], [Metric("m", "t", "x")], "secret"));

    [Fact]
    public void ShouldNotContainContent_WhenAnActivityNameLeaks_Fails() =>
        Should.Throw<ShouldAssertException>(() => SignalAssertions.ShouldNotContainContent([Activity("op-secret", "t", "v")], [], [], "secret"));

    [Fact]
    public void ShouldNotContainContent_WhenAnActivityTagLeaks_Fails() =>
        Should.Throw<ShouldAssertException>(() => SignalAssertions.ShouldNotContainContent([Activity("op", "t", "has-secret-inside")], [], [], "secret"));

    [Fact]
    public void ShouldNotContainContent_WhenALogMessageLeaks_Fails() =>
        Should.Throw<ShouldAssertException>(() => SignalAssertions.ShouldNotContainContent([], [Log("the secret", "k", "v")], [], "secret"));

    [Fact]
    public void ShouldNotContainContent_WhenALogFieldLeaks_Fails() =>
        Should.Throw<ShouldAssertException>(() => SignalAssertions.ShouldNotContainContent([], [Log("m", "k", "secret")], [], "secret"));

    [Fact]
    public void ShouldNotContainContent_WhenAMetricTagLeaks_Fails() =>
        Should.Throw<ShouldAssertException>(() => SignalAssertions.ShouldNotContainContent([], [], [Metric("m", "t", "secret")], "secret"));

    [Fact]
    public void ShouldNotContainContent_WhenAMetricNameLeaks_Fails() =>
        Should.Throw<ShouldAssertException>(() => SignalAssertions.ShouldNotContainContent([], [], [Metric("secret.metric", "t", "v")], "secret"));

    [Fact]
    public void ShouldNotContainContent_WhenANonStringTagFormatsToTheLiteral_Fails() =>
        Should.Throw<ShouldAssertException>(() => SignalAssertions.ShouldNotContainContent(
            [new ActivityObservation("op", ActivityStatusCode.Ok, new Dictionary<string, object?> { ["n"] = 4711 }.ToFrozenDictionary())], [], [], "4711"));

    [Fact]
    public void ShouldNotContainContent_WhenALiteralIsBlank_ThrowsArgumentExceptionNamingForbidden() =>
        Should.Throw<ArgumentException>(() => SignalAssertions.ShouldNotContainContent([], [], [], " ")).ParamName.ShouldBe("forbidden");

    [Fact]
    public void ShouldNotContainContent_WhenACollectionIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        Should.Throw<ArgumentNullException>(() => SignalAssertions.ShouldNotContainContent(null!, [], [], "x")).ParamName.ShouldBe("activities");
        Should.Throw<ArgumentNullException>(() => SignalAssertions.ShouldNotContainContent([], null!, [], "x")).ParamName.ShouldBe("logs");
        Should.Throw<ArgumentNullException>(() => SignalAssertions.ShouldNotContainContent([], [], null!, "x")).ParamName.ShouldBe("metrics");
    }

    private static ActivityObservation Activity(string name, string tag, string value) =>
        new(name, ActivityStatusCode.Ok, new Dictionary<string, object?> { [tag] = value }.ToFrozenDictionary());

    private static RecordingLogEntry Log(string message, string key, string value) =>
        new("category", LogLevel.Debug, new EventId(1), ImmutableDictionary<string, object?>.Empty.Add(key, value), message);

    private static MetricObservation Metric(string name, string tag, string value) =>
        new(name, new Dictionary<string, object?> { [tag] = value }, 1, null);
}
