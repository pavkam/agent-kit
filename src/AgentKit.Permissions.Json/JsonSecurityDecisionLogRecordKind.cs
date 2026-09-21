// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Discriminates newline-delimited records in the security decision-store log.</summary>
public enum JsonSecurityDecisionLogRecordKind
{
    /// <summary>One terminal decision was recorded append-only.</summary>
    Recorded = 0,
}
