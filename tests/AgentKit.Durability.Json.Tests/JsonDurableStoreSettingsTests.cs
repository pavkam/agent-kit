// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json.Tests;

/// <summary>Verifies the journal's evidence bounds, compaction policy, and encoding contract are validated eagerly.</summary>
public sealed class JsonDurableStoreSettingsTests
{
    /// <summary>Verifies a nonpositive record bound is refused, because no record can satisfy it.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumRecordBytesIsNotPositive_ThrowsForThatArgument(int maximumRecordBytes)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonDurableStoreSettings(
            maximumRecordBytes, 1_024, 16, JsonEncodingSettings.CreateDefault()));

        exception.ParamName.ShouldBe("maximumRecordBytes");
    }

    /// <summary>Verifies a nonpositive manifest bound is refused, because no manifest could be decoded.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumDocumentBytesIsNotPositive_ThrowsForThatArgument(int maximumDocumentBytes)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonDurableStoreSettings(
            1_024, maximumDocumentBytes, 16, JsonEncodingSettings.CreateDefault()));

        exception.ParamName.ShouldBe("maximumDocumentBytes");
    }

    /// <summary>Verifies a nonpositive compaction threshold is refused, because it would compact on every open.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenCompactionRecordThresholdIsNotPositive_ThrowsForThatArgument(int threshold)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonDurableStoreSettings(
            1_024, 1_024, threshold, JsonEncodingSettings.CreateDefault()));

        exception.ParamName.ShouldBe("compactionRecordThreshold");
    }

    /// <summary>Verifies the encoding contract is required, because the root's fingerprint is bound to it.</summary>
    [Fact]
    public void Constructor_WhenEncodingIsNull_ThrowsForTheEncodingArgument()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new JsonDurableStoreSettings(1_024, 1_024, 16, null!));

        exception.ParamName.ShouldBe("encoding");
    }

    /// <summary>Verifies accepted bounds are retained exactly, so appends and replay enforce what the host chose.</summary>
    [Fact]
    public void Constructor_WhenBoundsArePositive_RetainsThemExactly()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        var settings = new JsonDurableStoreSettings(2_048, 4_096, 32, encoding);

        settings.MaximumRecordBytes.ShouldBe(2_048);
        settings.MaximumDocumentBytes.ShouldBe(4_096);
        settings.CompactionRecordThreshold.ShouldBe(32);
        settings.Encoding.ShouldBe(encoding);
    }

    /// <summary>Verifies the documented defaults are conservative and use the canonical encoding contract.</summary>
    [Fact]
    public void CreateDefault_WhenCalled_UsesConservativeBoundsAndTheCanonicalEncoding()
    {
        var settings = JsonDurableStoreSettings.CreateDefault();

        settings.MaximumRecordBytes.ShouldBe(1_048_576);
        settings.MaximumDocumentBytes.ShouldBe(1_048_576);
        settings.CompactionRecordThreshold.ShouldBe(4_096);
        settings.Encoding.Fingerprint.ShouldBe(JsonEncodingSettings.CreateDefault().Fingerprint);
    }
}
