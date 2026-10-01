// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is one stored vector in a tenant partition of an index.</summary>
/// <param name="Tenant">The tenant partition.</param>
/// <param name="Record">The vector record.</param>
internal sealed record VectorEntry(TenantId Tenant, VectorRecord Record);
