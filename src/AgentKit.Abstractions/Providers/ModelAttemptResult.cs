// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one <see cref="IChatModel"/>
/// attempt, returned from <see cref="IChatModel.ExecuteAsync"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="ModelAttemptCompleted"/>, <see cref="ModelAttemptFailed"/>,
/// and <see cref="ModelAttemptCancelled"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a fourth kind. The returned value always
/// matches the same terminal event most recently delivered to the
/// attempt's <see cref="IModelResponseObserver"/>.
/// </remarks>
public abstract record ModelAttemptResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelAttemptResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected ModelAttemptResult()
    {
    }
}
