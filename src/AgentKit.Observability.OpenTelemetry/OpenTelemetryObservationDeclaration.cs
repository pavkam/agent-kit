// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Records one exporter key's captured snapshot in the service collection so repeated registration can be validated without static state.</summary>
/// <param name="Snapshot">The immutable snapshot the key's sinks close over.</param>
internal sealed record OpenTelemetryObservationDeclaration(OpenTelemetryObservationOptionsSnapshot Snapshot);
