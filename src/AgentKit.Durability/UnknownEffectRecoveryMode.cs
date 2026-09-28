// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Controls recovery when a non-idempotent effect's outcome is unknown.</summary>
public enum UnknownEffectRecoveryMode
{
    /// <summary>Require operator or reconciliation before any retry.</summary>
    RequireOperator,

    /// <summary>Attempt reconciliation when a backend supports it.</summary>
    ReconcileWhenSupported,
}
