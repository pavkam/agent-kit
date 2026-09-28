// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json.Tests;

/// <summary>Verifies the composition-time options carry the documented defaults before a host adjusts them.</summary>
public sealed class JsonDurableStoreOptionsTests
{
    /// <summary>Verifies an unconfigured instance matches the immutable defaults a registration would freeze.</summary>
    [Fact]
    public void Constructor_WhenCalled_MatchesTheDocumentedDefaultSettings()
    {
        var defaults = JsonDurableStoreSettings.CreateDefault();

        var options = new JsonDurableStoreOptions();

        options.MaximumRecordBytes.ShouldBe(defaults.MaximumRecordBytes);
        options.MaximumDocumentBytes.ShouldBe(defaults.MaximumDocumentBytes);
        options.CompactionRecordThreshold.ShouldBe(defaults.CompactionRecordThreshold);
        options.Encoding.Fingerprint.ShouldBe(defaults.Encoding.Fingerprint);
    }

    /// <summary>Verifies every value is mutable while a configure delegate runs.</summary>
    [Fact]
    public void Properties_WhenAssigned_RetainTheConfiguredValues()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        var options = new JsonDurableStoreOptions
        {
            MaximumRecordBytes = 512,
            MaximumDocumentBytes = 1_024,
            CompactionRecordThreshold = 8,
            Encoding = encoding,
        };

        options.MaximumRecordBytes.ShouldBe(512);
        options.MaximumDocumentBytes.ShouldBe(1_024);
        options.CompactionRecordThreshold.ShouldBe(8);
        options.Encoding.ShouldBe(encoding);
    }
}
