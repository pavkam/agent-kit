// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;
/// <summary>Verifies OutputSchemaPreflightManifest behavior and contracts.</summary>
public sealed class OutputSchemaPreflightManifestTests
{
    private static readonly JsonSchemaDialectId _dialect = new("urn:test:dialect");
    [Fact]
    public void OutputSchemaPreflightManifest_WhenObservedCountsExceedLimits_RejectsExactCount()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaPreflightManifest(Profile(), _dialect, new ContentHash("hash"), Limits(), 3, 1)).ParamName.ShouldBe("observedDepth");
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaPreflightManifest(Profile(), _dialect, new ContentHash("hash"), Limits(), 1, 3)).ParamName.ShouldBe("observedNodes");
    }

    [Fact]
    public void Constructor_WhenProfileIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputSchemaPreflightManifest(null!, _dialect, new ContentHash("hash"), Limits(), 1, 1)).ParamName.ShouldBe("profile");

    [Fact]
    public void Constructor_WhenDialectIsUnsupported_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputSchemaPreflightManifest(Profile(), new JsonSchemaDialectId("urn:test:other"), new ContentHash("hash"), Limits(), 1, 1)).ParamName.ShouldBe("dialect");

    [Fact]
    public void Constructor_WhenSchemaFingerprintIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputSchemaPreflightManifest(Profile(), _dialect, default, Limits(), 1, 1)).ParamName.ShouldBe("schemaFingerprint");

    [Fact]
    public void Constructor_WhenLimitsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputSchemaPreflightManifest(Profile(), _dialect, new ContentHash("hash"), null!, 1, 1)).ParamName.ShouldBe("limits");

    [Fact]
    public void Constructor_WhenObservedCountsAreNotPositive_ThrowsExactParameter()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaPreflightManifest(Profile(), _dialect, new ContentHash("hash"), Limits(), 0, 1)).ParamName.ShouldBe("observedDepth");
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaPreflightManifest(Profile(), _dialect, new ContentHash("hash"), Limits(), 1, 0)).ParamName.ShouldBe("observedNodes");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var profile = Profile();
        var fingerprint = new ContentHash("hash");
        var limits = Limits();
        var manifest = new OutputSchemaPreflightManifest(profile, _dialect, fingerprint, limits, 1, 1);
        manifest.Profile.ShouldBe(profile);
        manifest.Dialect.ShouldBe(_dialect);
        manifest.SchemaFingerprint.ShouldBe(fingerprint);
        manifest.Limits.ShouldBe(limits);
        manifest.ObservedDepth.ShouldBe(1);
        manifest.ObservedNodes.ShouldBe(1);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputSchemaPreflightManifest(Profile(), _dialect, new ContentHash("hash"), Limits(), 1, 1);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static OutputSchemaEngineProfile Profile(ImmutableArray<JsonSchemaDialectId>? dialects = null, ImmutableArray<string>? assertions = null, ImmutableArray<string>? annotations = null) => new(new OutputSchemaProfileId("test"), new OutputSchemaProfileVersion(1), _dialect, dialects ?? [_dialect], assertions ?? ["type"], annotations ?? ["title"]);
    private static OutputSchemaProcessingLimits Limits() => new(128, 2, 2);
}
