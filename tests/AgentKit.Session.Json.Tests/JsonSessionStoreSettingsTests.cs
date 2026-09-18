// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the immutable bounds, compaction policy, and encoding contract for the JSON session store.</summary>
public sealed class JsonSessionStoreSettingsTests
{
    /// <summary>Verifies every explicit bound and the encoding contract are retained unchanged.</summary>
    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsEveryValue()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        var settings = new JsonSessionStoreSettings(1_024, 2_048, 16, 8, encoding);

        settings.MaximumRecordBytes.ShouldBe(1_024);
        settings.MaximumDocumentBytes.ShouldBe(2_048);
        settings.CompactionRecordThreshold.ShouldBe(16);
        settings.MaximumIssuedReadSnapshots.ShouldBe(8);
        settings.Encoding.ShouldBeSameAs(encoding);
    }

    /// <summary>Verifies a non-positive maximum record byte bound is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumRecordBytesIsNotPositive_ThrowsExactParameter(int value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionStoreSettings(
                value, 1_024, 16, 8, JsonEncodingSettings.CreateDefault()))
            .ParamName.ShouldBe("maximumRecordBytes");

    /// <summary>Verifies a non-positive maximum document byte bound is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumDocumentBytesIsNotPositive_ThrowsExactParameter(int value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionStoreSettings(
                1_024, value, 16, 8, JsonEncodingSettings.CreateDefault()))
            .ParamName.ShouldBe("maximumDocumentBytes");

    /// <summary>Verifies a non-positive compaction threshold is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenCompactionRecordThresholdIsNotPositive_ThrowsExactParameter(int value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionStoreSettings(
                1_024, 1_024, value, 8, JsonEncodingSettings.CreateDefault()))
            .ParamName.ShouldBe("compactionRecordThreshold");

    /// <summary>Verifies a non-positive issued-snapshot bound is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumIssuedReadSnapshotsIsNotPositive_ThrowsExactParameter(int value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionStoreSettings(
                1_024, 1_024, 16, value, JsonEncodingSettings.CreateDefault()))
            .ParamName.ShouldBe("maximumIssuedReadSnapshots");

    /// <summary>Verifies a null encoding contract is rejected.</summary>
    [Fact]
    public void Constructor_WhenEncodingIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new JsonSessionStoreSettings(1_024, 1_024, 16, 8, null!))
            .ParamName.ShouldBe("encoding");

    /// <summary>Verifies the conservative defaults advertised for local applications.</summary>
    [Fact]
    public void CreateDefault_WhenCalled_ReturnsConservativeLocalDefaults()
    {
        var defaults = JsonSessionStoreSettings.CreateDefault();

        defaults.MaximumRecordBytes.ShouldBe(1_048_576);
        defaults.MaximumDocumentBytes.ShouldBe(1_048_576);
        defaults.CompactionRecordThreshold.ShouldBe(4_096);
        defaults.MaximumIssuedReadSnapshots.ShouldBe(4_096);
        defaults.Encoding.ShouldBe(JsonEncodingSettings.CreateDefault());
    }

    /// <summary>Verifies two settings built from identical evidence compare equal.</summary>
    [Fact]
    public void Equals_WhenAllFieldsMatch_IsEqual()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        var left = new JsonSessionStoreSettings(1_024, 1_024, 16, 8, encoding);
        var right = new JsonSessionStoreSettings(1_024, 1_024, 16, 8, encoding);

        left.ShouldBe(right);
    }

    /// <summary>Verifies a differing bound breaks equality.</summary>
    [Fact]
    public void Equals_WhenMaximumRecordBytesDiffers_IsNotEqual()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        var left = new JsonSessionStoreSettings(1_024, 1_024, 16, 8, encoding);
        var right = new JsonSessionStoreSettings(2_048, 1_024, 16, 8, encoding);

        left.ShouldNotBe(right);
    }
}
