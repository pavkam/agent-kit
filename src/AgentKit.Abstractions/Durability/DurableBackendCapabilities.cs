// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes what one durable backend adapter actually supports.</summary>
/// <param name="SupportsDistributedOwnership">Whether the backend can coordinate cross-process ownership.</param>
/// <param name="SupportsExternalHandoff">Whether the backend can accept external workflow handoff.</param>
/// <param name="SupportsReconciliation">Whether the backend exposes reconciliation for unknown effects.</param>
public readonly record struct DurableBackendCapabilities(
    bool SupportsDistributedOwnership,
    bool SupportsExternalHandoff,
    bool SupportsReconciliation);
