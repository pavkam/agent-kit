// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed outcome of one durable reconciliation attempt.</summary>
public abstract record DurableReconciliationResult
{
    /// <summary>Initializes a reconciliation result.</summary>
    private protected DurableReconciliationResult()
    {
    }
}
