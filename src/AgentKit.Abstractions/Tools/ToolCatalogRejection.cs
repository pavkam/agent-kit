// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Rejects the entire catalog exposure without publishing a partial graph.</summary>
/// <remarks>The originating merge context retains collision evidence. This decision invents no replacement registration and carries no untrusted diagnostic text.</remarks>
public sealed record ToolCatalogRejection: ToolCatalogMergeDecision;
