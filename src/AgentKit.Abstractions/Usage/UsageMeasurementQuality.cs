// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Distinguishes the evidential source of a usage amount from report lifecycle.</summary>
public enum UsageMeasurementQuality
{
    /// <summary>The applicable amount is unavailable; it is not reported zero.</summary>
    Unknown,
    /// <summary>The framework or host directly measured the amount.</summary>
    Measured,
    /// <summary>The serving provider reported the amount without a local estimate.</summary>
    ProviderReported,
    /// <summary>A declared estimator or pricing source calculated the amount.</summary>
    Estimated,
    /// <summary>The source explicitly declares that this dimension does not apply to its contribution.</summary>
    NotApplicable,
}
