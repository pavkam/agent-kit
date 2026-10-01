// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Reports whether one captured content field survived redaction or was omitted, and why.</summary>
/// <param name="Content">The redacted content to export, or <see langword="null"/> when the field was omitted.</param>
/// <param name="OmittedReason">The bounded omission reason, or <see langword="null"/> when <paramref name="Content"/> is present.</param>
internal readonly record struct OpenTelemetryContentCaptureOutcome(ObservationContent? Content, string? OmittedReason)
{
    /// <summary>Gets whether the field was omitted.</summary>
    internal bool IsOmitted => Content is null;
}
