// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Is the persisted form of the common session-entry fields shared by both goal entry kinds.</summary>
/// <param name="Id">The entry identity.</param>
/// <param name="AgentId">The addressed agent.</param>
/// <param name="SessionId">The addressed session.</param>
/// <param name="Correlation">The bounded correlation kind: <c>before_run</c>, <c>in_run</c>, or <c>after_run</c>.</param>
/// <param name="OperationId">The operation identity.</param>
/// <param name="RunId">The run for an in-run correlation or the causal run for an after-run correlation.</param>
/// <param name="TurnId">The turn for an in-run correlation, or <see langword="null"/>.</param>
/// <param name="AdmissionId">The admission for a before-run correlation, or <see langword="null"/>.</param>
/// <param name="BranchId">The branch.</param>
/// <param name="Sequence">The position on the branch.</param>
/// <param name="CausalParentId">The preceding entry, or <see langword="null"/>.</param>
/// <param name="RecordedAt">The commit instant.</param>
/// <param name="SchemaVersion">The entry schema version.</param>
internal sealed record GoalEntryEnvelopeDocument(
    Guid Id,
    Guid AgentId,
    Guid SessionId,
    string Correlation,
    Guid OperationId,
    Guid? RunId,
    Guid? TurnId,
    Guid? AdmissionId,
    Guid BranchId,
    long Sequence,
    Guid? CausalParentId,
    DateTimeOffset RecordedAt,
    string SchemaVersion)
{
    /// <summary>Captures the common fields of an entry.</summary>
    /// <param name="entry">The non-null entry.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException">The entry carries an unsupported correlation kind.</exception>
    internal static GoalEntryEnvelopeDocument From(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var (kind, run, turn, admission) = entry.Correlation switch
        {
            BeforeRunOperationCorrelation before => ("before_run", (Guid?) null, (Guid?) null, before.AdmissionId?.Value),
            InRunOperationCorrelation inRun => ("in_run", inRun.RunId.Value, inRun.TurnId?.Value, null),
            AfterRunOperationCorrelation after => ("after_run", after.CausalRunId.Value, null, null),
            _ => throw new ArgumentException("The entry carries an unsupported correlation kind.", nameof(entry)),
        };
        return new(
            entry.Id.Value,
            entry.Address.AgentId.Value,
            entry.Address.SessionId.Value,
            kind,
            entry.Correlation.OperationId.Value,
            run,
            turn,
            admission,
            entry.BranchId.Value,
            entry.Sequence.Value,
            entry.CausalParentId?.Value,
            entry.RecordedAt,
            entry.SchemaVersion.Value);
    }

    /// <summary>Restores the operation correlation.</summary>
    /// <returns>The correlation.</returns>
    /// <exception cref="InvalidDataException">The kind is unknown or a required member is absent.</exception>
    internal OperationCorrelation ToCorrelation() => Correlation switch
    {
        "before_run" => new BeforeRunOperationCorrelation(new OperationId(OperationId), AdmissionId is { } admission ? new AdmissionId(admission) : null),
        "in_run" when RunId is { } run => new InRunOperationCorrelation(new OperationId(OperationId), new RunId(run), TurnId is { } turn ? new TurnId(turn) : null),
        "after_run" when RunId is { } causal => new AfterRunOperationCorrelation(new OperationId(OperationId), new RunId(causal)),
        _ => throw new InvalidDataException("The persisted goal entry carries an unknown or incomplete correlation."),
    };

    /// <summary>Restores the session address.</summary>
    /// <returns>The address.</returns>
    internal SessionAddress ToAddress() => new(new AgentId(AgentId), new SessionId(SessionId));
}
