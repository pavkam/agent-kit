// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one model selection.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="ModelSelected"/>, <see cref="NoCompatibleModel"/>, and
/// <see cref="InvalidModelPolicy"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a fourth kind and defeat exhaustive handling
/// in the loop.
/// </remarks>
public abstract record ModelSelectionResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelSelectionResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend the
    /// hierarchy.
    /// </summary>
    private protected ModelSelectionResult()
    {
    }
}
