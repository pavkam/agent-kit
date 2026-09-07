// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the protected operation whose exact effect is being authorized.</summary>
public enum SecurityOperationKind
{
    /// <summary>Observes file content or metadata.</summary>
    FileRead,
    /// <summary>Observes directory entries or metadata.</summary>
    DirectoryRead,
    /// <summary>Traverses file names and observes bounded file content for matching.</summary>
    FileSearch,
    /// <summary>Creates or mutates file content.</summary>
    FileWrite,
    /// <summary>Creates a directory.</summary>
    DirectoryCreate,
    /// <summary>Starts or controls a host process.</summary>
    Process,
    /// <summary>Performs network name resolution, connection, or request work.</summary>
    Network,
    /// <summary>Mutates typed application or session state.</summary>
    StateMutation,
    /// <summary>Delegates work or authority to a child attempt.</summary>
    Delegation,
    /// <summary>Observes typed application or session state without mutating it.</summary>
    StateRead,
    /// <summary>Stages, publishes, reads, or deletes durable artifact content.</summary>
    Artifact,
}
