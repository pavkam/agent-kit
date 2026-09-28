// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Declares which OpenTelemetry signal families one keyed exporter registration enables.</summary>
[Flags]
public enum OpenTelemetrySignalSet
{
    /// <summary>Exports no optional signals beyond structural defaults.</summary>
    None = 0,

    /// <summary>Exports causal activities for run and audit events.</summary>
    Activities = 1,

    /// <summary>Exports bounded counters and histograms.</summary>
    Metrics = 2,

    /// <summary>Exports structured diagnostic logs.</summary>
    Logs = 4,

    /// <summary>Exports security audit records through the configured audit exporter.</summary>
    Audit = 8,
}
