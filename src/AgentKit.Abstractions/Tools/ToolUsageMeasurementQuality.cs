// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes what a reported tool-usage amount means.</summary>
public enum ToolUsageMeasurementQuality
{
    /// <summary>The amount was measured.</summary>
    Measured,
    /// <summary>The amount was estimated.</summary>
    Estimated,
    /// <summary>The dimension was applicable but its amount is unknown.</summary>
    Unknown,
    /// <summary>The dimension was not applicable.</summary>
    NotApplicable,
}
