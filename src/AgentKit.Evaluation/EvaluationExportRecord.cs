// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Records what one report exporter answered when the report was published to it.</summary>
public sealed record EvaluationExportRecord
{
    /// <summary>Initializes a validated record.</summary>
    /// <param name="key">The exporter key.</param>
    /// <param name="result">The typed exporter answer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    public EvaluationExportRecord(EvaluationReportExporterKey key, EvaluationExportResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(result);
        Key = key;
        Result = result;
    }

    /// <summary>Gets the exporter key.</summary>
    public EvaluationReportExporterKey Key { get; }

    /// <summary>Gets the typed exporter answer.</summary>
    public EvaluationExportResult Result { get; }
}
