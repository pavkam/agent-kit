// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares how an <see cref="IToolExecutor"/> or <see cref="IToolScheduler"/> settles a batch when one call fails.</summary>
/// <remarks>
/// This is host-configured runtime policy, not a per-tool declaration; individual tools never select their sibling's
/// failure behavior. Under the tool scheduling and concurrency contract, failure of one parallel call must not erase
/// sibling results unless the host explicitly configures fail-fast settlement.
/// </remarks>
public enum ToolBatchFailureMode
{
    /// <summary>Every accepted call reaches its own terminal result independently of sibling outcomes.</summary>
    SettleIndependently,

    /// <summary>A failed call signals cancellation to still-running siblings, which still settle to a terminal result.</summary>
    FailFast,
}
