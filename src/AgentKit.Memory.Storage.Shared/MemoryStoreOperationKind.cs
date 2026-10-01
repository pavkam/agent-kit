// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Names the bounded store operations used as a trace and metric dimension.</summary>
internal enum MemoryStoreOperationKind
{
    /// <summary>A record creation or version publication.</summary>
    Write = 0,

    /// <summary>A point read.</summary>
    Read = 1,

    /// <summary>A page read.</summary>
    List = 2,

    /// <summary>A lifecycle transition.</summary>
    Transition = 3,

    /// <summary>A deletion.</summary>
    Delete = 4,

    /// <summary>An active-version switch.</summary>
    Activate = 5,

    /// <summary>A vector batch upsert.</summary>
    Upsert = 6,

    /// <summary>A vector search.</summary>
    Search = 7,
}
