// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Declares where a plan's results are persisted and which exporters receive its report.</summary>
/// <remarks>A plan with no result store keeps its results only in the returned report; there is no hidden durable store. Exporters are separate effects and never share a transaction with the result store.</remarks>
public sealed record EvaluationRecordingPolicy
{
    /// <summary>Gets the policy that persists nothing and exports nowhere.</summary>
    public static EvaluationRecordingPolicy None { get; } = new(null, []);

    /// <summary>Initializes a validated policy.</summary>
    /// <param name="resultStore">The registered store every case result is appended to, or <see langword="null"/> to persist nothing.</param>
    /// <param name="exporters">The registered exporters the finished report is published to, in publication order; an empty array is valid.</param>
    /// <exception cref="ArgumentException"><paramref name="resultStore"/> is blank, or <paramref name="exporters"/> is the default array or contains a blank or repeated key.</exception>
    public EvaluationRecordingPolicy(EvaluationResultStoreKey? resultStore, ImmutableArray<EvaluationReportExporterKey> exporters)
    {
        if (resultStore is { } store)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(store.Value, nameof(resultStore));
        }

        ArgumentException.ThrowIfDefault(exporters);
        HashSet<EvaluationReportExporterKey> keys = [];
        foreach (var exporter in exporters)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(exporter.Value, nameof(exporters));
            ArgumentException.ThrowIfNotEqual(keys.Add(exporter), true, nameof(exporters));
        }

        ResultStore = resultStore;
        Exporters = exporters;
    }

    /// <summary>Gets the store every case result is appended to, or <see langword="null"/> when nothing is persisted.</summary>
    public EvaluationResultStoreKey? ResultStore { get; }

    /// <summary>Gets the exporters the finished report is published to, in publication order.</summary>
    public ImmutableArray<EvaluationReportExporterKey> Exporters { get; }

    /// <inheritdoc/>
    public bool Equals(EvaluationRecordingPolicy? other) =>
        other is not null && ResultStore == other.ResultStore && Exporters.SequenceEqual(other.Exporters);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ResultStore);
        foreach (var exporter in Exporters)
        {
            hash.Add(exporter);
        }

        return hash.ToHashCode();
    }
}
