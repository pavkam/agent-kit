// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The observable condition that caused a compaction attempt to be requested.</summary>
public enum CompactionTriggerKind
{
    /// <summary>Estimated context size is approaching a configured limit.</summary>
    ContextPressure,

    /// <summary>A provider rejected a request for exceeding its context window.</summary>
    ProviderOverflow,

    /// <summary>An operator or maintenance process explicitly requested compaction.</summary>
    ExplicitMaintenance,

    /// <summary>The effective instructions changed in a way that starts a new context epoch.</summary>
    InstructionEpochChanged
}
