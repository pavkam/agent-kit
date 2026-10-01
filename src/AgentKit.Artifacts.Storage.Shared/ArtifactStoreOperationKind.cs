// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Names the artifact store operations observed under one bounded label.</summary>
internal enum ArtifactStoreOperationKind
{
    /// <summary>Staging bytes.</summary>
    Prepare = 0,

    /// <summary>Publishing a preparation.</summary>
    Finalize = 1,

    /// <summary>Aborting a preparation.</summary>
    Abort = 2,

    /// <summary>Opening committed bytes.</summary>
    Read = 3,

    /// <summary>Deleting a committed version.</summary>
    Delete = 4,

    /// <summary>Recording a reference-commit intent.</summary>
    IntentRecord = 5,

    /// <summary>Reading a reference-commit intent.</summary>
    IntentGet = 6,

    /// <summary>Conditionally transitioning a reference-commit intent.</summary>
    IntentTransition = 7,

    /// <summary>Listing pending reference-commit intents.</summary>
    IntentListPending = 8,
}
