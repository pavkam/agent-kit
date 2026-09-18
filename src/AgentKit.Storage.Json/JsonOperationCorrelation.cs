// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of the closed <see cref="OperationCorrelation"/> hierarchy, flattened into one discriminated document.</summary>
/// <remarks>
/// <para>
/// The three concrete correlation kinds do not share a member set, so this mirror carries the union of their identities and
/// leaves the members that do not apply to <see cref="Kind"/> as <see langword="null"/>. <see cref="FromDomain"/> always
/// writes that exact projection, and <see cref="ToDomain"/> reads only the members the selected kind defines, so a document
/// never smuggles an admission receipt into an in-run correlation or a turn into an after-run correlation.
/// </para>
/// <para>
/// Every member is a JSON-native primitive. Domain identities such as <see cref="AgentKit.OperationId"/> and
/// <see cref="AgentKit.RunId"/> are unwrapped to their underlying <see cref="Guid"/> values here and rebuilt through their own
/// validating constructors on read, so a persisted empty GUID is rejected rather than reconstructed as a default identity.
/// </para>
/// <para>
/// This type is an immutable value with structural equality over its primitive members and is safe to share across threads.
/// </para>
/// </remarks>
/// <param name="Kind">The discriminator selecting which concrete correlation the document reconstructs.</param>
/// <param name="OperationId">The raw value of the causal operation identity, required and non-empty for every kind.</param>
/// <param name="AdmissionId">The raw value of the causing admission receipt for <see cref="JsonOperationCorrelationKind.BeforeRun"/>, or <see langword="null"/> when admission did not cause the operation or the kind is not before-run.</param>
/// <param name="RunId">The raw value of the active run for <see cref="JsonOperationCorrelationKind.InRun"/> or of the settled causal run for <see cref="JsonOperationCorrelationKind.AfterRun"/>; <see langword="null"/> for <see cref="JsonOperationCorrelationKind.BeforeRun"/>.</param>
/// <param name="TurnId">The raw value of the active turn for a turn-scoped <see cref="JsonOperationCorrelationKind.InRun"/> correlation, or <see langword="null"/> for run-scoped work and for the other kinds.</param>
public sealed record JsonOperationCorrelation(
    JsonOperationCorrelationKind Kind,
    Guid OperationId,
    Guid? AdmissionId,
    Guid? RunId,
    Guid? TurnId)
{
    /// <summary>Projects one domain correlation into its portable discriminated JSON representation.</summary>
    /// <param name="value">The non-null correlation to project; it must be one of the three kinds declared by AgentKit.Abstractions.</param>
    /// <returns>A document whose <see cref="Kind"/> names the concrete source type and whose inapplicable identity members are <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not one of the closed correlation kinds, which can only happen if the hierarchy is extended.</exception>
    public static JsonOperationCorrelation FromDomain(OperationCorrelation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            BeforeRunOperationCorrelation before => new JsonOperationCorrelation(
                JsonOperationCorrelationKind.BeforeRun,
                before.OperationId.Value,
                before.AdmissionId?.Value,
                null,
                null),
            InRunOperationCorrelation inRun => new JsonOperationCorrelation(
                JsonOperationCorrelationKind.InRun,
                inRun.OperationId.Value,
                null,
                inRun.RunId.Value,
                inRun.TurnId?.Value),
            AfterRunOperationCorrelation after => new JsonOperationCorrelation(
                JsonOperationCorrelationKind.AfterRun,
                after.OperationId.Value,
                null,
                after.CausalRunId.Value,
                null),
            _ => throw new ArgumentException(
                "Value must be one of the closed OperationCorrelation kinds declared by AgentKit.Abstractions.",
                nameof(value)),
        };
    }

    /// <summary>Reconstructs the exact domain correlation this document was projected from.</summary>
    /// <returns>A correlation whose concrete type is selected by <see cref="Kind"/> and whose identities equal the projected originals.</returns>
    /// <remarks>
    /// Reconstruction goes through the domain identity constructors and the concrete correlation constructor, so every
    /// invariant is revalidated against untrusted persisted bytes rather than assumed from the writer. A document that names
    /// <see cref="JsonOperationCorrelationKind.InRun"/> or <see cref="JsonOperationCorrelationKind.AfterRun"/> without a
    /// <see cref="RunId"/> is rejected, because the run identity is required evidence for those kinds.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A persisted identity is empty, a required run identity is missing, or <see cref="Kind"/> is not a defined <see cref="JsonOperationCorrelationKind"/> value.</exception>
    public OperationCorrelation ToDomain()
    {
        var operationId = new OperationId(OperationId);
        return Kind switch
        {
            JsonOperationCorrelationKind.BeforeRun => new BeforeRunOperationCorrelation(
                operationId,
                AdmissionId is { } admissionId ? new AdmissionId(admissionId) : null),
            JsonOperationCorrelationKind.InRun => new InRunOperationCorrelation(
                operationId,
                new RunId(RunId.GetValueOrDefault()),
                TurnId is { } turnId ? new TurnId(turnId) : null),
            JsonOperationCorrelationKind.AfterRun => new AfterRunOperationCorrelation(
                operationId,
                new RunId(RunId.GetValueOrDefault())),
            _ => throw new ArgumentOutOfRangeException(
                nameof(Kind),
                Kind,
                "The persisted correlation kind is not a defined JsonOperationCorrelationKind value."),
        };
    }
}
