// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the bounded outcomes of an intent-store operation.</summary>
public enum ArtifactReferenceCommitIntentOutcome
{
    /// <summary>The intent was recorded or transitioned now.</summary>
    Applied,
    /// <summary>An equivalent earlier operation had already produced this state.</summary>
    Replayed,
    /// <summary>The intent differs from the one already recorded, or the requested transition is not legal.</summary>
    Conflict,
    /// <summary>No intent exists for the tenant and preparation.</summary>
    NotFound,
    /// <summary>The intent was not in the expected state, so another actor won the conditional transition.</summary>
    StateChanged,
}
