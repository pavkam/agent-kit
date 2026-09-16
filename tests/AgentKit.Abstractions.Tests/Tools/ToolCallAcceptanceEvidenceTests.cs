// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolCallAcceptanceEvidence behavior and contracts.</summary>
public sealed class ToolCallAcceptanceEvidenceTests
{
    [Fact]
    public void ToolCallAcceptanceEvidence_Constructor_WhenFingerprintDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallAcceptanceEvidence(new GrantId(Guid.NewGuid()), default, DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("validatedArgumentsFingerprint");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var grantId = new GrantId(Guid.NewGuid());
        var fingerprint = new InputFingerprint("sha256:accepted");
        var evidence = new ToolCallAcceptanceEvidence(grantId, fingerprint, DateTimeOffset.UnixEpoch);
        evidence.InvocationGrantId.ShouldBe(grantId);
        evidence.ValidatedArgumentsFingerprint.ShouldBe(fingerprint);
        evidence.AcceptedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolCallAcceptanceEvidence(new GrantId(Guid.NewGuid()), new InputFingerprint("sha256:accepted"), DateTimeOffset.UnixEpoch);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
