// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json;

/// <summary>Mutable composition-time input for the evidence bounds and compaction policy of one JSON durable journal.</summary>
/// <remarks>
/// Instances exist only while a configure delegate passed to a registration extension runs. The registration
/// materializes the mutated values into an immutable, eagerly validated <see cref="JsonDurableStoreSettings"/> and
/// never registers this type in dependency injection.
/// </remarks>
public sealed class JsonDurableStoreOptions
{
    /// <summary>Gets or sets the maximum encoded transition-record size.</summary>
    /// <value>A positive byte count. Defaults to one mebibyte.</value>
    public int MaximumRecordBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum encoded manifest size.</summary>
    /// <value>A positive byte count. Defaults to one mebibyte.</value>
    public int MaximumDocumentBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the replayed-record count that triggers compaction.</summary>
    /// <value>A positive record count. Defaults to 4096.</value>
    public int CompactionRecordThreshold { get; set; } = 4_096;

    /// <summary>Gets or sets the frozen JSON encoding contract bound to the store root.</summary>
    /// <value>The canonical contract by default; replacing it changes the fingerprint the root is bound to.</value>
    public JsonEncodingSettings Encoding { get; set; } = JsonEncodingSettings.CreateDefault();
}
