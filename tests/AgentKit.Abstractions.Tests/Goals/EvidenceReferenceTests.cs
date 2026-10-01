// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies EvidenceReference constraints.</summary>
public sealed class EvidenceReferenceTests
{
    [Fact]
    public void Constructor_WhenKindOrReferenceIsBlank_ThrowsWithTheExactParameterName()
    {
        Should.Throw<ArgumentException>(() => new EvidenceReference(" ", "r")).ParamName.ShouldBe("kind");
        Should.Throw<ArgumentException>(() => new EvidenceReference("k", "")).ParamName.ShouldBe("reference");
    }

    [Fact]
    public void Constructor_WhenFingerprintIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvidenceReference("k", "r", default(ContentHash))).ParamName.ShouldBe("fingerprint");

    [Fact]
    public void Constructor_WhenFingerprintIsOmitted_LeavesItNull() => new EvidenceReference("k", "r").Fingerprint.ShouldBeNull();
}
