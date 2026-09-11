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

    private static OutputSchemaEngineProfile Profile(ImmutableArray<JsonSchemaDialectId>? dialects = null, ImmutableArray<string>? assertions = null, ImmutableArray<string>? annotations = null) => new(new OutputSchemaProfileId("test"), new OutputSchemaProfileVersion(1), _dialect, dialects ?? [_dialect], assertions ?? ["type"], annotations ?? ["title"]);
    private static OutputSchemaProcessingLimits Limits() => new(128, 2, 2);
}
