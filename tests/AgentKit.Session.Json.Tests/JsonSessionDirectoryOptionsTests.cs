// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the mutable composition-time options agree with the immutable settings defaults.</summary>
public sealed class JsonSessionDirectoryOptionsTests
{
    /// <summary>Verifies every default value equals the corresponding immutable settings default.</summary>
    [Fact]
    public void Constructor_WhenCreated_DefaultsEqualCreateDefaultSettings()
    {
        var options = new JsonSessionDirectoryOptions();
        var defaults = JsonSessionDirectorySettings.CreateDefault();

        options.MaximumRecordBytes.ShouldBe(defaults.MaximumRecordBytes);
        options.MaximumDocumentBytes.ShouldBe(defaults.MaximumDocumentBytes);
        options.CompactionRecordThreshold.ShouldBe(defaults.CompactionRecordThreshold);
    }

    /// <summary>Verifies materializing the unmodified defaults produces settings equal to <c>CreateDefault</c>.</summary>
    [Fact]
    public void Constructor_WhenDefaultsAreMaterialized_ProducesSettingsEqualToCreateDefault()
    {
        var options = new JsonSessionDirectoryOptions();

        var settings = new JsonSessionDirectorySettings(
            options.MaximumRecordBytes, options.MaximumDocumentBytes, options.CompactionRecordThreshold,
            new JsonEncodingSettings(options.Encoding.SerializerOptions));

        settings.ShouldBe(JsonSessionDirectorySettings.CreateDefault());
    }

    /// <summary>Verifies assigning an invalid value does not validate eagerly, since validation is deferred to settings.</summary>
    [Fact]
    public void Setters_WhenAssigned_DoNotValidate()
    {
        var options = new JsonSessionDirectoryOptions
        {
            MaximumRecordBytes = -1,
            MaximumDocumentBytes = 0,
            CompactionRecordThreshold = -1,
        };

        options.MaximumRecordBytes.ShouldBe(-1);
        options.MaximumDocumentBytes.ShouldBe(0);
        options.CompactionRecordThreshold.ShouldBe(-1);
    }

    /// <summary>Verifies the mutable encoding contract exposes a real serializer-options instance a host may mutate.</summary>
    [Fact]
    public void Encoding_WhenAccessed_ExposesMutableSerializerOptions()
    {
        var options = new JsonSessionDirectoryOptions();

        options.Encoding.SerializerOptions.WriteIndented = true;

        options.Encoding.SerializerOptions.WriteIndented.ShouldBeTrue();
    }
}
