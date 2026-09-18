// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Mutable composition-time input for the bounds, compaction policy, and JSON encoding of one session-directory leaf.</summary>
/// <remarks>
/// Instances exist only while a configure delegate passed to
/// <see cref="ServiceExtensions.AddJsonSessionDirectory(IServiceCollection, ComponentId, JsonSessionDirectoryTarget, Action{JsonSessionDirectoryOptions}?)"/>
/// runs. The registration materializes the mutated values into an immutable, eagerly validated
/// <see cref="JsonSessionDirectorySettings"/> and never registers this type in dependency injection. The type performs no
/// validation of its own; invalid values are rejected by the settings constructor at registration.
/// </remarks>
public sealed class JsonSessionDirectoryOptions
{
    /// <summary>Gets or sets the maximum encoded routing-record size.</summary>
    /// <value>A positive byte count enforced before an append and while replaying the log. Defaults to one mebibyte.</value>
    public int MaximumRecordBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum encoded manifest size.</summary>
    /// <value>A positive byte count enforced before the manifest document is decoded. Defaults to one mebibyte.</value>
    public int MaximumDocumentBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the replayed-record count that triggers log compaction at initialization.</summary>
    /// <value>A positive count that bounds replay cost as the root accumulates routes. Defaults to 4096.</value>
    public int CompactionRecordThreshold { get; set; } = 4_096;

    /// <summary>Gets the mutable JSON encoding contract for this directory.</summary>
    /// <value>
    /// The encoding options whose <see cref="JsonEncodingOptions.SerializerOptions"/> a host may freely replace or mutate.
    /// The directory layers its required session converters onto the result and fingerprints the whole effective contract
    /// into the manifest.
    /// </value>
    public JsonEncodingOptions Encoding { get; } = new();
}
