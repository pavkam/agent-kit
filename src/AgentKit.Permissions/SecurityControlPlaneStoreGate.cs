// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Tracks trusted control-plane bootstrap evidence and schema readiness for durable security stores.</summary>
/// <remarks>
/// Writes fail closed until both schema initialization completes and the host records explicit bootstrap evidence.
/// </remarks>
internal sealed class SecurityControlPlaneStoreGate
{
    private readonly Lock _syncRoot = new();
    private SecurityControlPlaneBootstrap? _bootstrap;
    private bool _schemaInitialized;

    /// <summary>Records host-supplied bootstrap evidence before or after schema initialization.</summary>
    /// <param name="bootstrap">The non-null bounded bootstrap capability evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bootstrap"/> is null.</exception>
    internal void RecordBootstrap(SecurityControlPlaneBootstrap bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        lock (_syncRoot)
        {
            _bootstrap = bootstrap;
        }
    }

    /// <summary>Marks schema initialization complete for this store instance.</summary>
    internal void MarkSchemaInitialized()
    {
        lock (_syncRoot)
        {
            _schemaInitialized = true;
        }
    }

    /// <summary>Requires bootstrap evidence and completed initialization before mutating store state.</summary>
    /// <exception cref="SecurityGrantStoreUnavailableException">Bootstrap evidence is missing or schema is not initialized.</exception>
    internal void RequireReadyForWrites()
    {
        lock (_syncRoot)
        {
            if (!_schemaInitialized || _bootstrap is null)
            {
                throw new SecurityGrantStoreUnavailableException(
                    SecurityGrantStoreFailureKind.OpenFailed,
                    "The security control-plane store was used before trusted bootstrap initialization.");
            }
        }
    }

    /// <summary>Completes bootstrap by recording evidence and marking schema initialization in one step.</summary>
    /// <param name="bootstrap">The non-null bounded bootstrap capability evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bootstrap"/> is null.</exception>
    internal void CompleteBootstrap(SecurityControlPlaneBootstrap bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        lock (_syncRoot)
        {
            _bootstrap = bootstrap;
            _schemaInitialized = true;
        }
    }
}
