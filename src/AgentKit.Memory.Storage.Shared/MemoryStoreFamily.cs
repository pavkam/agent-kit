// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Names the three independent state axes a store adapter serves, used as a bounded trace and metric dimension.</summary>
internal enum MemoryStoreFamily
{
    /// <summary>Durable memory records.</summary>
    Memory = 0,

    /// <summary>Versioned documents and chunk sets.</summary>
    Document = 1,

    /// <summary>Vector indexes.</summary>
    Vector = 2,
}
