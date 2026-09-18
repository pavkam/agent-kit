// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Captures one fixed local JSON store root supplied through the trusted budget bootstrap boundary.</summary>
/// <remarks>
/// The value authorizes no ordinary file operation. Construction validates configuration without probing or creating the
/// root; initialization performs the explicitly selected effects. Link and persistent store-identity checks detect
/// accidental or ordinary path replacement but are not operating-system isolation; trusted bootstrap retains responsibility
/// for directory ownership, access control, and target confinement.
/// </remarks>
public sealed record JsonBudgetLedgerTarget
{
    /// <summary>Initializes immutable bootstrap configuration for one exact ledger root.</summary>
    /// <param name="directoryPath">The fully qualified local root directory interpreted only as a path.</param>
    /// <param name="expectedStoreInstanceId">The nondefault identity that the initialized manifest must contain.</param>
    /// <param name="openMode">Whether the exact root and its manifest may be created.</param>
    /// <param name="recoveryMode">Whether an incomplete trailing append may be discarded during initialization.</param>
    /// <exception cref="ArgumentNullException"><paramref name="directoryPath"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="directoryPath"/> is blank or relative.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expectedStoreInstanceId"/> is default, an enumeration is undefined, or creation is combined with validation-only recovery.</exception>
    /// <remarks>
    /// Combining <see cref="JsonStoreOpenMode.CreateIfMissing"/> with <see cref="JsonStoreRecoveryMode.ValidateExact"/> is
    /// rejected, because a host that permits creating a root has already accepted that initialization may write to it, and
    /// refusing recovery in that mode would leave a crashed ledger permanently unopenable without manual intervention.
    /// </remarks>
    public JsonBudgetLedgerTarget(
        string directoryPath,
        JsonBudgetLedgerInstanceId expectedStoreInstanceId,
        JsonStoreOpenMode openMode,
        JsonStoreRecoveryMode recoveryMode)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedStoreInstanceId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(openMode);
        ArgumentOutOfRangeException.ThrowIfUndefined(recoveryMode);
        ArgumentOutOfRangeException.ThrowIfEqual(
            (openMode, recoveryMode),
            (JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.ValidateExact),
            nameof(recoveryMode));
        if (!Path.IsPathFullyQualified(directoryPath))
        {
            throw new ArgumentException(
                "The JSON budget-ledger root must be a fully qualified path.", nameof(directoryPath));
        }

        DirectoryPath = Path.GetFullPath(directoryPath);
        ExpectedStoreInstanceId = expectedStoreInstanceId;
        OpenMode = openMode;
        RecoveryMode = recoveryMode;
    }

    /// <summary>Gets the normalized fully qualified store root directory.</summary>
    /// <value>The fixed root path; callers must treat it as sensitive bootstrap configuration and never emit it to ordinary diagnostics.</value>
    public string DirectoryPath { get; }

    /// <summary>Gets the expected persistent store identity.</summary>
    /// <value>The exact nondefault identity verified against the manifest before the ledger serves any operation.</value>
    public JsonBudgetLedgerInstanceId ExpectedStoreInstanceId { get; }

    /// <summary>Gets the root creation permission.</summary>
    /// <value>The explicit bootstrap open mode; creation is a declared effect and never inferred.</value>
    public JsonStoreOpenMode OpenMode { get; }

    /// <summary>Gets the torn-append recovery permission.</summary>
    /// <value>The explicit validation or recovery mode applied to the transition journal during initialization.</value>
    public JsonStoreRecoveryMode RecoveryMode { get; }
}
