// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the terminal outcome of one language-intelligence query.</summary>
public enum LanguageQueryStatus
{
    /// <summary>The provider completed successfully, including a valid empty result.</summary>
    Success,
    /// <summary>The exact query grant was rejected before protected observation.</summary>
    Denied,
    /// <summary>The selected provider does not support this operation or document.</summary>
    Unsupported,
    /// <summary>The provider is not ready or available for the captured workspace.</summary>
    Unavailable,
    /// <summary>The provider observed a document version that is no longer current.</summary>
    Stale,
    /// <summary>The bounded query duration elapsed.</summary>
    TimedOut,
    /// <summary>The caller cancelled after the provider may have begun observation.</summary>
    Cancelled,
    /// <summary>The provider failed without producing a valid result snapshot.</summary>
    Failed,
}
