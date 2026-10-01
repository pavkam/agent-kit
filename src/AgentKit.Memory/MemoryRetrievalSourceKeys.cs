// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Names the first-party retrieval sources that <see cref="ServiceExtensions"/> can register.</summary>
/// <remarks>A key only selects a registration. Nothing is registered until the application calls the matching registration method, and a profile must still name the key explicitly.</remarks>
public static class MemoryRetrievalSourceKeys
{
    /// <summary>Gets the key of the durable-memory keyword source registered by <see cref="ServiceExtensions.AddDurableMemoryRetrievalSource(IServiceCollection)"/>.</summary>
    /// <value>A non-empty, stable source key.</value>
    public static RetrievalSourceKey DurableMemory { get; } = DurableMemoryRetrievalSource.Key;

    /// <summary>Gets the key of the document-chunk vector source registered by <see cref="ServiceExtensions.AddDocumentRetrievalSource(IServiceCollection)"/>.</summary>
    /// <value>A non-empty, stable source key.</value>
    public static RetrievalSourceKey Documents { get; } = DocumentChunkRetrievalSource.Key;
}
