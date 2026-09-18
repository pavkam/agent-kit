// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Mutable composition-time input for the bounds, compaction policy, and JSON encoding of one session-store leaf.</summary>
/// <remarks>
/// Instances exist only while a configure delegate passed to
/// <see cref="ServiceExtensions.AddJsonSessionStore(IServiceCollection, JsonSessionStoreTarget, Action{JsonSessionStoreOptions}?)"/>
/// runs. The registration materializes the mutated values into an immutable, eagerly validated
/// <see cref="JsonSessionStoreSettings"/> and never registers this type in dependency injection, so the captured bounds and
/// encoding contract cannot drift after composition. The type performs no validation of its own; invalid values are
/// rejected by the settings constructor with <see cref="ArgumentOutOfRangeException"/> at registration.
/// </remarks>
public sealed class JsonSessionStoreOptions
{
    /// <summary>Gets or sets the maximum encoded transition-record size.</summary>
    /// <value>A positive byte count enforced before an append and while replaying the log. Defaults to one mebibyte.</value>
    public int MaximumRecordBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum encoded manifest size.</summary>
    /// <value>A positive byte count enforced before the manifest document is decoded. Defaults to one mebibyte.</value>
    public int MaximumDocumentBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the replayed-record count that triggers log compaction at initialization.</summary>
    /// <value>A positive count that bounds replay cost as the root accumulates transitions. Defaults to 4096.</value>
    public int CompactionRecordThreshold { get; set; } = 4_096;

    /// <summary>Gets or sets how many distinct adapter-issued paged-read snapshots one store instance retains.</summary>
    /// <value>A positive count bounding process-local continuation provenance. Defaults to 4096.</value>
    public int MaximumIssuedReadSnapshots { get; set; } = 4_096;

    /// <summary>Gets the mutable JSON encoding contract for this store.</summary>
    /// <value>
    /// The encoding options whose <see cref="JsonEncodingOptions.SerializerOptions"/> a host may freely replace or mutate.
    /// The store layers its required session converters onto the result and fingerprints the whole effective contract into
    /// the manifest, so a root written under one contract is never decoded under another.
    /// </value>
    public JsonEncodingOptions Encoding { get; } = new();
}
