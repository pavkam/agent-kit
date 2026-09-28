// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies the journaled run-admission manifest's argument constraints and value semantics.</summary>
public sealed class DurableRunAdmissionManifestTests
{
    private static readonly Guid RunId = Guid.Parse("27000000-0000-0000-0000-000000000001");
    private static readonly Guid SessionId = Guid.Parse("28000000-0000-0000-0000-000000000001");
    private static readonly Guid AdmissionId = Guid.Parse("29000000-0000-0000-0000-000000000001");

    [Fact]
    public void Constructor_WhenRunIdIsEmpty_ThrowsForTheRunIdArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableRunAdmissionManifest(Guid.Empty, SessionId, AdmissionId))
            .ParamName.ShouldBe("runId");

    [Fact]
    public void Constructor_WhenSessionIdIsEmpty_ThrowsForTheSessionIdArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableRunAdmissionManifest(RunId, Guid.Empty, AdmissionId))
            .ParamName.ShouldBe("sessionId");

    [Fact]
    public void Constructor_WhenAdmissionIdIsEmpty_ThrowsForTheAdmissionIdArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableRunAdmissionManifest(RunId, SessionId, Guid.Empty))
            .ParamName.ShouldBe("admissionId");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsEveryProperty()
    {
        var manifest = new DurableRunAdmissionManifest(RunId, SessionId, AdmissionId);

        manifest.RunId.ShouldBe(RunId);
        manifest.SessionId.ShouldBe(SessionId);
        manifest.AdmissionId.ShouldBe(AdmissionId);
    }

    [Fact]
    public void Equals_WhenEveryFieldAgrees_IsStructural() =>
        new DurableRunAdmissionManifest(RunId, SessionId, AdmissionId)
            .ShouldBe(new DurableRunAdmissionManifest(RunId, SessionId, AdmissionId));
}
