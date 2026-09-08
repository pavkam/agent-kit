// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Marks a safe idle evaluation boundary with no fabricated turn or response evidence.</summary>
public sealed record IdleContinuationBoundary: RunContinuationBoundary;
