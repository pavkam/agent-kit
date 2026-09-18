// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the immutable bounds, compaction policy, and encoding contract for the JSON session directory.</summary>
public sealed class JsonSessionDirectorySettingsTests
{
    /// <summary>Verifies every explicit bound and the encoding contract are retained unchanged.</summary>
    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsEveryValue()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        var settings = new JsonSessionDirectorySettings(1_024, 2_048, 16, encoding);

        settings.MaximumRecordBytes.ShouldBe(1_024);
        settings.MaximumDocumentBytes.ShouldBe(2_048);
        settings.CompactionRecordThreshold.ShouldBe(16);
        settings.Encoding.ShouldBeSameAs(encoding);
    }

    /// <summary>Verifies a non-positive maximum record byte bound is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumRecordBytesIsNotPositive_ThrowsExactParameter(int value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionDirectorySettings(
                value, 1_024, 16, JsonEncodingSettings.CreateDefault()))
            .ParamName.ShouldBe("maximumRecordBytes");

    /// <summary>Verifies a non-positive maximum document byte bound is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumDocumentBytesIsNotPositive_ThrowsExactParameter(int value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionDirectorySettings(
                1_024, value, 16, JsonEncodingSettings.CreateDefault()))
            .ParamName.ShouldBe("maximumDocumentBytes");

    /// <summary>Verifies a non-positive compaction threshold is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenCompactionRecordThresholdIsNotPositive_ThrowsExactParameter(int value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionDirectorySettings(
                1_024, 1_024, value, JsonEncodingSettings.CreateDefault()))
            .ParamName.ShouldBe("compactionRecordThreshold");

    /// <summary>Verifies a null encoding contract is rejected.</summary>
    [Fact]
    public void Constructor_WhenEncodingIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new JsonSessionDirectorySettings(1_024, 1_024, 16, null!))
            .ParamName.ShouldBe("encoding");

    /// <summary>Verifies the conservative defaults advertised for local applications.</summary>
    [Fact]
    public void CreateDefault_WhenCalled_ReturnsConservativeLocalDefaults()
    {
        var defaults = JsonSessionDirectorySettings.CreateDefault();

        defaults.MaximumRecordBytes.ShouldBe(1_048_576);
        defaults.MaximumDocumentBytes.ShouldBe(1_048_576);
        defaults.CompactionRecordThreshold.ShouldBe(4_096);
        defaults.Encoding.ShouldBe(JsonEncodingSettings.CreateDefault());
    }

    /// <summary>Verifies two settings built from identical evidence compare equal.</summary>
    [Fact]
    public void Equals_WhenAllFieldsMatch_IsEqual()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        var left = new JsonSessionDirectorySettings(1_024, 1_024, 16, encoding);
        var right = new JsonSessionDirectorySettings(1_024, 1_024, 16, encoding);

        left.ShouldBe(right);
    }

    /// <summary>Verifies a differing bound breaks equality.</summary>
    [Fact]
    public void Equals_WhenMaximumRecordBytesDiffers_IsNotEqual()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        var left = new JsonSessionDirectorySettings(1_024, 1_024, 16, encoding);
        var right = new JsonSessionDirectorySettings(2_048, 1_024, 16, encoding);

        left.ShouldNotBe(right);
    }
}
