// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records one explicit, ordered repair applied while producing a history view.</summary>
public sealed record HistoryRepair
{
    /// <summary>Creates immutable repair evidence.</summary>
    /// <param name="sourceMessageIds">The initialized, nonempty, unique source-message identities affected by the repair.</param>
    /// <param name="kind">The defined normative repair operation.</param>
    /// <param name="reason">The nonblank content-safe repair reason.</param>
    /// <param name="diagnostics">The nonnull, content-safe structured repair diagnostics.</param>
    /// <exception cref="ArgumentException">The source identities are default, empty, default-valued, or duplicated, or the reason is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    public HistoryRepair(ImmutableArray<MessageId> sourceMessageIds, HistoryRepairKind kind, string reason, ExtensionData diagnostics)
    {
        ArgumentException.ThrowIfDefaultEmptyOrDuplicate(sourceMessageIds);
        ArgumentOutOfRangeException.ThrowIfNotEqual(Enum.IsDefined(kind), true, nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(diagnostics);
        SourceMessageIds = sourceMessageIds;
        Kind = kind;
        Reason = reason;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the affected source-message identities in source order.</summary><value>An initialized, nonempty, unique sequence.</value>
    public ImmutableArray<MessageId> SourceMessageIds { get; }
    /// <summary>Gets the repair operation.</summary><value>A defined normative operation.</value>
    public HistoryRepairKind Kind { get; }
    /// <summary>Gets the content-safe reason for the repair.</summary><value>A nonblank reason.</value>
    public string Reason { get; }
    /// <summary>Gets structured supporting diagnostics.</summary><value>Nonnull, content-safe extension data.</value>
    public ExtensionData Diagnostics { get; }

    /// <summary>Compares complete repair evidence, including ordered arrays.</summary><param name="other">The repair to compare.</param><returns>True when every scalar and ordered element is equal.</returns>
    public bool Equals(HistoryRepair? other) => other is not null && Kind == other.Kind && Reason == other.Reason && SourceMessageIds.SequenceEqual(other.SourceMessageIds) && Diagnostics.Equals(other.Diagnostics);

    /// <summary>Returns a hash compatible with complete ordered equality.</summary><returns>A hash over every scalar and array element.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind); hash.Add(Reason);
        foreach (var id in SourceMessageIds)
        {
            hash.Add(id);
        }

        hash.Add(Diagnostics);
        return hash.ToHashCode();
    }
}
