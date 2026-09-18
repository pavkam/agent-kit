// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies the mutable composition-time approval-store options mirror the immutable settings defaults.</summary>
public sealed class JsonApprovalStoreOptionsTests
{
    /// <summary>Verifies a fresh options instance materializes to settings equal to the documented defaults.</summary>
    [Fact]
    public void Constructor_WhenUnmodified_MatchesSettingsDefaults()
    {
        var expected = JsonApprovalStoreSettings.CreateDefault();

        var options = new JsonApprovalStoreOptions();
        var actual = new JsonApprovalStoreSettings(
            options.MaximumRecordBytes,
            options.MaximumDocumentBytes,
            options.CompactionRecordThreshold,
            new JsonEncodingSettings(options.Encoding.SerializerOptions));

        actual.ShouldBe(expected);
        options.MaximumRecordBytes.ShouldBe(expected.MaximumRecordBytes);
        options.MaximumDocumentBytes.ShouldBe(expected.MaximumDocumentBytes);
        options.CompactionRecordThreshold.ShouldBe(expected.CompactionRecordThreshold);
    }

    /// <summary>Verifies every scalar property is independently settable so a configure delegate can adjust one bound at a time.</summary>
    [Fact]
    public void Properties_WhenSet_RetainAssignedValues()
    {
        var options = new JsonApprovalStoreOptions
        {
            MaximumRecordBytes = 1,
            MaximumDocumentBytes = 2,
            CompactionRecordThreshold = 3,
        };

        options.MaximumRecordBytes.ShouldBe(1);
        options.MaximumDocumentBytes.ShouldBe(2);
        options.CompactionRecordThreshold.ShouldBe(3);
    }

    /// <summary>Verifies the encoding contract is a stable get-only mutable instance a configure delegate can adjust in place.</summary>
    [Fact]
    public void Encoding_WhenAccessedRepeatedly_ReturnsSameMutableInstance()
    {
        var options = new JsonApprovalStoreOptions();

        var first = options.Encoding;
        var second = options.Encoding;

        _ = first.ShouldNotBeNull();
        first.ShouldBeSameAs(second);
    }
}
