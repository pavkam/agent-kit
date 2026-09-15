// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>The availability a vendor feed reported for a <see cref="KnownModel"/> at import time.</summary>
/// <remarks>
/// The status is reference data, not a runtime fact: a provider may retire a model between catalog
/// imports, and only the provider's own response proves whether a request succeeds.
/// </remarks>
public enum KnownModelStatus
{
    /// <summary>The vendor lists the model as generally available.</summary>
    Available,

    /// <summary>The vendor lists the model as a preview whose behavior or pricing may still change.</summary>
    Preview,

    /// <summary>The vendor has announced the model's retirement; <see cref="KnownModel.ReplacedBy"/> may name a successor.</summary>
    Deprecated,

    /// <summary>The vendor no longer serves the model.</summary>
    Disabled,
}
