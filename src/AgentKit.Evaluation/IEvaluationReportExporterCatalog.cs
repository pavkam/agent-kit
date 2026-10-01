// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Resolves registered report exporters by their stable key.</summary>
/// <remarks>Exporters are additive and keyed; a plan names the keys it publishes to, and the runner resolves each before any effect so an unknown key fails plan validation.</remarks>
public interface IEvaluationReportExporterCatalog
{
    /// <summary>Finds the exporter registered under a key.</summary>
    /// <param name="key">The non-blank exporter key.</param>
    /// <returns>The exporter, or <see langword="null"/> when none is registered.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    public IEvaluationReportExporter? Find(EvaluationReportExporterKey key);
}
