// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no creation route is visible for the authorized retry identity.</summary>
/// <remarks>Directories use this result for absent and tenant-masked idempotency routes, without exposing a session address before one exists.</remarks>
public sealed record SessionCreationLocationNotFound: SessionCreationLocationResult;
