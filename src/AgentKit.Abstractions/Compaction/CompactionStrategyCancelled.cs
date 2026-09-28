// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Checkpoint production was cancelled before it completed.</summary>
/// <param name="Cancellation">The cancellation evidence.</param>
public sealed record CompactionStrategyCancelled(CompactionCancellation Cancellation): CompactionStrategyResult;
