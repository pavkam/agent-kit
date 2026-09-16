// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolCallAdmissionEvidence behavior and contracts.</summary>
public sealed class ToolCallAdmissionEvidenceTests
{
    [Fact]
    public void ToolCallAdmissionEvidence_Constructor_WhenOrdinalNegative_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallAdmissionEvidence(new ToolCatalogVersion("catalog"), -1, new InputFingerprint("sha256:raw")));
        exception.ParamName.ShouldBe("sourceOrdinal");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var version = new ToolCatalogVersion("catalog");
        var fingerprint = new InputFingerprint("sha256:raw");
        var evidence = new ToolCallAdmissionEvidence(version, 3, fingerprint);
        evidence.CatalogVersion.ShouldBe(version);
        evidence.SourceOrdinal.ShouldBe(3);
        evidence.RawArgumentsFingerprint.ShouldBe(fingerprint);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolCallAdmissionEvidence(new ToolCatalogVersion("catalog"), 3, new InputFingerprint("sha256:raw"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
