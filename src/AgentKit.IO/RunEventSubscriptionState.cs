// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Identifies the local delivery state of a non-owning event subscription.</summary>
internal enum RunEventSubscriptionState
{
    /// <summary>The subscriber may receive further events.</summary>
    Active,
    /// <summary>The publisher ended normally; already buffered events remain available until drained or explicitly abandoned.</summary>
    Completed,
    /// <summary>The consumer explicitly disposed or abandoned enumeration and released its buffer.</summary>
    Disposed,
    /// <summary>The enumeration token cancelled only this subscription.</summary>
    Cancelled,
    /// <summary>The bounded queue filled; delivery terminated explicitly and its pending buffer was released.</summary>
    SlowConsumer,
    /// <summary>The run-owned hub was disposed before normal event completion.</summary>
    HubDisposed,
}
