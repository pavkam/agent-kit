// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Captures authorization evidence by binding an exact profile publication to one scope and identity.</summary>
/// <remarks>Selectors do not authorize effects or issue grants; callers must separately activate the captured authority and submit an exact security request.</remarks>
public interface ISecurityProfileSelector
{
    /// <summary>Captures authorization for one exact operation.</summary>
    /// <param name="request">The non-null exact scope, identity, profile, and composition request.</param>
    /// <param name="cancellationToken">Cancels before capture completes.</param>
    /// <returns>Fresh matching authorization evidence or a typed unavailable result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before capture completes.</exception>
    public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(
        SecurityAuthorizationCaptureRequest request,
        CancellationToken cancellationToken = default);
}
