// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Portable JSON mirror of the closed <see cref="BudgetReconciliationEvidence"/> hierarchy, flattened into one discriminated document.</summary>
/// <remarks>
/// Only the measured and estimated kinds carry a quantity, so <see cref="Actual"/> is null for the other two and is omitted
/// from the encoded line under the canonical contract. Reconstruction reads the quantity only for the kinds that define it,
/// so a document can never smuggle a usage value into proof-of-no-usage evidence and silently settle a reservation the
/// caller asked to release.
/// </remarks>
/// <param name="Kind">The discriminator selecting which concrete evidence the document reconstructs.</param>
/// <param name="Actual">The nonnegative reconciled usage for a measured or estimated kind, or <see langword="null"/> otherwise.</param>
public sealed record JsonBudgetReconciliationEvidence(JsonBudgetReconciliationEvidenceKind Kind, decimal? Actual)
{
    /// <summary>Projects one domain reconciliation evidence value into its portable discriminated representation.</summary>
    /// <param name="value">The non-null evidence to project; it must be one of the four closed kinds.</param>
    /// <returns>A document whose <see cref="Kind"/> names the concrete source type and whose quantity is present only when the kind defines one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not one of the closed evidence kinds, which can only happen if the hierarchy is extended.</exception>
    public static JsonBudgetReconciliationEvidence FromDomain(BudgetReconciliationEvidence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            BudgetActualMeasured measured =>
                new JsonBudgetReconciliationEvidence(JsonBudgetReconciliationEvidenceKind.Measured, measured.Actual),
            BudgetActualEstimated estimated =>
                new JsonBudgetReconciliationEvidence(JsonBudgetReconciliationEvidenceKind.Estimated, estimated.Actual),
            BudgetNoUsageProven =>
                new JsonBudgetReconciliationEvidence(JsonBudgetReconciliationEvidenceKind.NoUsageProven, null),
            BudgetStillUnknown =>
                new JsonBudgetReconciliationEvidence(JsonBudgetReconciliationEvidenceKind.StillUnknown, null),
            _ => throw new ArgumentException(
                "Value must be one of the closed BudgetReconciliationEvidence kinds declared by AgentKit.Abstractions.",
                nameof(value)),
        };
    }

    /// <summary>Reconstructs the exact domain evidence this document was projected from.</summary>
    /// <returns>Evidence whose concrete type is selected by <see cref="Kind"/> and whose quantity equals the projected original.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Kind"/> is undefined or a quantity-bearing kind has a negative persisted quantity.</exception>
    /// <exception cref="ArgumentException">A quantity-bearing kind omits its required quantity.</exception>
    public BudgetReconciliationEvidence ToDomain() => Kind switch
    {
        JsonBudgetReconciliationEvidenceKind.Measured => new BudgetActualMeasured(RequireActual()),
        JsonBudgetReconciliationEvidenceKind.Estimated => new BudgetActualEstimated(RequireActual()),
        JsonBudgetReconciliationEvidenceKind.NoUsageProven => new BudgetNoUsageProven(),
        JsonBudgetReconciliationEvidenceKind.StillUnknown => new BudgetStillUnknown(),
        _ => throw new ArgumentOutOfRangeException(
            nameof(Kind), Kind, "The persisted reconciliation evidence kind is not a defined value."),
    };

    private decimal RequireActual()
    {
        Debug.Assert(
            Kind is JsonBudgetReconciliationEvidenceKind.Measured or JsonBudgetReconciliationEvidenceKind.Estimated,
            "Only measured and estimated evidence define a reconciled quantity.");
        return Actual ?? throw new ArgumentException(
            "A persisted quantity-bearing reconciliation record omits its actual usage.", nameof(Actual));
    }
}
