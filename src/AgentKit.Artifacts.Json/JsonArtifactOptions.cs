// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json;

/// <summary>Configures the bounds and encoding contract of a JSON artifact store.</summary>
public sealed class JsonArtifactOptions
{
    /// <summary>Gets or sets the largest encoded log record, in bytes.</summary>
    /// <value>A positive bound. The default is one mebibyte; artifact bytes are stored as separate payload files.</value>
    public int MaximumRecordBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the largest encoded manifest document, in bytes.</summary>
    /// <value>A positive bound. The default is one mebibyte.</value>
    public int MaximumDocumentBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the largest payload file read back, in bytes.</summary>
    /// <value>A positive bound no larger than <see cref="int.MaxValue"/>. The default is 64 MiB; the coordinator's own limit normally keeps artifacts well below it.</value>
    public int MaximumPayloadBytes { get; set; } = 64 * 1_024 * 1_024;

    /// <summary>Gets or sets the record count above which initialization compacts the log to one snapshot per entry.</summary>
    /// <value>A positive threshold. The default is 4096.</value>
    public int CompactionRecordThreshold { get; set; } = 4_096;

    /// <summary>Gets or sets the JSON encoding contract.</summary>
    /// <value>The strict canonical contract by default.</value>
    public JsonEncodingSettings Encoding { get; set; } = JsonEncodingSettings.CreateDefault();
}
