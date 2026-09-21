// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>One newline-delimited append-only transition in the security decision-store record log.</summary>
/// <param name="Kind">The record discriminator.</param>
/// <param name="Decision">The complete terminal decision, present for <see cref="JsonSecurityDecisionLogRecordKind.Recorded"/>.</param>
public sealed record JsonSecurityDecisionLogRecord(
    JsonSecurityDecisionLogRecordKind Kind,
    JsonSecurityDecision? Decision)
{
    /// <summary>Creates the record describing one terminal decision entering the store.</summary>
    /// <param name="decision">The complete immutable terminal decision evidence.</param>
    /// <returns>A recorded transition carrying the full decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null.</exception>
    public static JsonSecurityDecisionLogRecord ForRecorded(SecurityDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new JsonSecurityDecisionLogRecord(
            JsonSecurityDecisionLogRecordKind.Recorded,
            JsonSecurityDecision.FromDomain(decision));
    }
}
