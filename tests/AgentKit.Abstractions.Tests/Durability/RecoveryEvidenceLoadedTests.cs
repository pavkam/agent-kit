// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryEvidenceLoaded behavior and contracts.</summary>
public sealed class RecoveryEvidenceLoadedTests
{
    [Fact]
    public void RecoveryEvidenceLoaded_Constructor_WhenEvidenceIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new RecoveryEvidenceLoaded(null!));
        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsEvidence()
    {
        var evidence = DurabilityTestData.Evidence();
        var loaded = new RecoveryEvidenceLoaded(evidence);
        loaded.Evidence.ShouldBeSameAs(evidence);
        RecoveryEvidenceResult result = loaded;
        _ = result.ShouldBeOfType<RecoveryEvidenceLoaded>();
    }

    [Fact]
    public void With_WhenEvidenceIsNull_ThrowsArgumentNullException()
    {
        var loaded = new RecoveryEvidenceLoaded(DurabilityTestData.Evidence());
        Should.Throw<ArgumentNullException>(() => _ = loaded with { Evidence = null! }).ParamName.ShouldBe("Evidence");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RecoveryEvidenceLoaded(DurabilityTestData.Evidence());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenEvidenceIsValid_UpdatesEvidence()
    {
        var original = new RecoveryEvidenceLoaded(DurabilityTestData.Evidence());
        var newEvidence = DurabilityTestData.Evidence();
        var updated = original with { Evidence = newEvidence };
        updated.Evidence.ShouldBeSameAs(newEvidence);
    }
}
