// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// What happened to one candidate alias while selection walked the policy's
/// candidate list.
/// </summary>
/// <remarks>
/// These outcomes distinguish configuration mistakes from genuine capability
/// gaps. An alias that is simply absent from the catalog is a very different
/// operational problem from one whose model cannot stream.
/// </remarks>
public enum ModelCandidateOutcome
{
    /// <summary>The candidate was chosen.</summary>
    Selected,

    /// <summary>
    /// The catalog publishes no conversational model under this alias,
    /// normally a configuration or spelling error.
    /// </summary>
    NotInCatalog,

    /// <summary>
    /// The model exists but lacks at least one required capability.
    /// </summary>
    MissingRequiredCapability,

    /// <summary>
    /// The model exists but its context window is smaller than the request's
    /// stated minimum.
    /// </summary>
    InsufficientContextWindow,

    /// <summary>
    /// The candidate was never examined because an earlier candidate was
    /// selected, or because the policy forbids fallback.
    /// </summary>
    NotEvaluated,
}
