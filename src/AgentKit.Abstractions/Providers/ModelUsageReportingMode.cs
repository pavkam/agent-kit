// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares which provider usage-report phases one configured operation can report.</summary>
/// <remarks>This is availability evidence, not a guarantee that every successful operation supplies complete counters.</remarks>
public enum ModelUsageReportingMode
{
    /// <summary>The provider exposes no usage report for the operation.</summary>
    NotReported = 0,

    /// <summary>The provider can expose a final usage report only.</summary>
    TerminalOnly = 1,

    /// <summary>The provider can expose interim reports but no final usage report.</summary>
    InterimOnly = 2,

    /// <summary>The provider can expose interim reports and a final usage report.</summary>
    StreamingAndTerminal = 3,
}
