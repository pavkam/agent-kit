// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports an atomically committed promotion or its idempotent replay.</summary>
public sealed record InputPromoted: InputPromotionResult
{
    /// <summary>Initializes committed promotion evidence.</summary>
    public InputPromoted(InputPromotionSnapshot snapshot, ImmutableArray<AdmittedInput> promoted, SessionVersion sessionVersion)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentOutOfRangeException.ThrowIfEqual(promoted.IsDefault, true, nameof(promoted));
        ArgumentOutOfRangeException.ThrowIfEqual(promoted.Length == snapshot.AdmissionIds.Length, false, nameof(promoted));
        Snapshot = snapshot; Promoted = promoted; SessionVersion = sessionVersion;
    }
    /// <summary>Gets exact plan evidence.</summary><value>The committed or reconciled snapshot.</value>
    public InputPromotionSnapshot Snapshot { get; }
    /// <summary>Gets promoted records.</summary><value>Records in committed selection order.</value>
    public ImmutableArray<AdmittedInput> Promoted { get; }
    /// <summary>Gets resulting version.</summary><value>The post-commit session version.</value>
    public SessionVersion SessionVersion { get; }
}
