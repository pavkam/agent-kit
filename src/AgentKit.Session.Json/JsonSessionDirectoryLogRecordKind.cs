// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Discriminates the authoritative routing transitions appended to the directory record log.</summary>
/// <remarks>
/// Values start at one so a truncated or zero-filled record fails closed rather than decoding as a route write. Each
/// enumeration name is persisted as text, so adding a kind is backward compatible while renaming one is a breaking schema
/// change that must advance the directory schema version.
/// </remarks>
public enum JsonSessionDirectoryLogRecordKind
{
    /// <summary>Records one conditional route write against an already established session address.</summary>
    RouteRecorded = 1,

    /// <summary>Records one creation route committed before the selected store is invoked.</summary>
    CreationRouteRecorded = 2,
}
