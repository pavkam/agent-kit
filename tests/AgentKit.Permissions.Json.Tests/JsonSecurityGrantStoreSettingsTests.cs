// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies JsonSecurityGrantStoreSettings bound validation, defaults, and value semantics.</summary>
public sealed class JsonSecurityGrantStoreSettingsTests
{
    /// <summary>Verifies every non-positive byte bound or compaction threshold is rejected with its exact parameter name.</summary>
    [Theory]
    [InlineData(0, 1, 1, "maximumRecordBytes")]
    [InlineData(-1, 1, 1, "maximumRecordBytes")]
    [InlineData(1, 0, 1, "maximumDocumentBytes")]
    [InlineData(1, -1, 1, "maximumDocumentBytes")]
    [InlineData(1, 1, 0, "compactionRecordThreshold")]
    [InlineData(1, 1, -1, "compactionRecordThreshold")]
    public void Constructor_WhenBoundIsNotPositive_ThrowsExactArgument(
        int maximumRecordBytes, int maximumDocumentBytes, int compactionRecordThreshold, string paramName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonSecurityGrantStoreSettings(
            maximumRecordBytes, maximumDocumentBytes, compactionRecordThreshold, JsonEncodingSettings.CreateDefault()));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(paramName);
    }

    /// <summary>Verifies a null encoding contract is rejected with the exact parameter name.</summary>
    [Fact]
    public void Constructor_WhenEncodingIsNull_ThrowsArgumentNull()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new JsonSecurityGrantStoreSettings(1, 1, 1, null!));

        exception.ParamName.ShouldBe("encoding");
    }

    /// <summary>Verifies valid bounds and the encoding contract are retained exactly.</summary>
    [Fact]
    public void Constructor_WhenValid_RetainsEveryBound()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        var settings = new JsonSecurityGrantStoreSettings(10, 20, 30, encoding);

        settings.MaximumRecordBytes.ShouldBe(10);
        settings.MaximumDocumentBytes.ShouldBe(20);
        settings.CompactionRecordThreshold.ShouldBe(30);
        settings.Encoding.ShouldBeSameAs(encoding);
    }

    /// <summary>Verifies default settings expose the documented conservative bounds and support non-destructive copying.</summary>
    [Fact]
    public void CreateDefault_WhenCopiedWithNoChanges_RetainsEveryBound()
    {
        var original = JsonSecurityGrantStoreSettings.CreateDefault();

        original.MaximumRecordBytes.ShouldBe(1_048_576);
        original.MaximumDocumentBytes.ShouldBe(1_048_576);
        original.CompactionRecordThreshold.ShouldBe(4_096);

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
    }
}
