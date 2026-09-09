// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates an isolated budget ledger for each reusable contract scenario.</summary>
public interface IBudgetLedgerConformanceFixture
{
    /// <summary>Creates a fresh ledger whose clock and identities are isolated from other cases.</summary>
    /// <returns>A non-null ledger.</returns>
    public IBudgetLedger CreateLedger();

    /// <summary>Advances the deterministic ledger clock.</summary>
    /// <param name="delta">The positive elapsed duration.</param>
    public void Advance(TimeSpan delta);

    /// <summary>Arms the next ledger clock read to throw.</summary>
    public void ArmClockFailure();

    /// <summary>Arms the next dimension lookup to throw.</summary>
    public void ArmCatalogFailure();

    /// <summary>Arms reservation identity generation to throw on its second subsequent call.</summary>
    public void ArmSecondReservationIdFailure();

    /// <summary>Arms reservation identity generation to cancel the supplied source on its second subsequent call.</summary>
    /// <param name="source">The source observed by the in-flight ledger request.</param>
    public void ArmSecondReservationIdCancellation(CancellationTokenSource source);

    /// <summary>Arms scope identity generation to cancel the supplied source before returning an identity.</summary>
    /// <param name="source">The source observed by the in-flight scope creation.</param>
    public void ArmScopeIdCancellation(CancellationTokenSource source);
}
