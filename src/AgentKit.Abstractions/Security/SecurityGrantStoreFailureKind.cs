// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies bounded infrastructure failures reported by an authoritative security-grant store.</summary>
/// <remarks>The classification is safe for control flow and diagnostics; it never carries provider messages, storage targets, or protected evidence.</remarks>
public enum SecurityGrantStoreFailureKind
{
    /// <summary>The configured store target could not be opened.</summary>
    OpenFailed,
    /// <summary>The store could not acquire its bounded concurrency lock.</summary>
    Busy,
    /// <summary>The store schema or codec is unsupported by this implementation.</summary>
    SchemaUnsupported,
    /// <summary>Persisted authoritative evidence is corrupt or internally inconsistent.</summary>
    CorruptEvidence,
    /// <summary>An authoritative mutation or read failed for another persistence reason.</summary>
    PersistenceFailed,
}
