// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Resolves report exporters from their explicit keyed registrations in the owning composition.</summary>
internal sealed class DefaultEvaluationReportExporterCatalog: IEvaluationReportExporterCatalog
{
    private readonly IServiceProvider _services;

    /// <summary>Initializes the catalog over the composition that holds keyed exporters.</summary>
    /// <param name="services">The provider that holds keyed exporters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public DefaultEvaluationReportExporterCatalog(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <inheritdoc/>
    public IEvaluationReportExporter? Find(EvaluationReportExporterKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        return _services.GetKeyedService<IEvaluationReportExporter>(key.Value);
    }
}
