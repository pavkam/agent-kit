// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Commits exact version-conditional file replacements with atomic target visibility.</summary>
public interface IAtomicFileReplacer
{
    /// <summary>Gets the component audience for replacement grants.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Attempts one authorized conditional replacement.</summary>
    /// <param name="request">The exact mutation request.</param>
    /// <param name="cancellationToken">Cancels before the commit gate; cancellation after commit is reported as committed.</param>
    /// <returns>The typed terminal result.</returns>
    public ValueTask<AtomicFileReplaceResult> ReplaceAsync(
        AtomicFileReplaceRequest request,
        CancellationToken cancellationToken = default);
}
