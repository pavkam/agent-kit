// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An opaque, disposable holder of provider secret material that applies authentication to one outgoing request and
/// then releases the secret.
/// </summary>
/// <remarks>
/// <para>
/// A lease exposes no accessor for the secret: the only operation is <see cref="ApplyAsync"/>, which writes the
/// authentication headers the credential implies onto an <see cref="IProviderAuthenticationTarget"/>. Implementations
/// override <see cref="object.ToString"/> so no textual form contains secret material, and release their references to
/// secret material when disposed, zeroing any byte buffer they own. Immutable managed strings cannot be zeroed, so a
/// lease limits their reachability instead. Applying a disposed lease throws <see cref="ObjectDisposedException"/>.
/// </para>
/// <para>
/// The receiver disposes the lease exactly once as soon as the credential has been applied. A lease is not shared across
/// attempts or retries, and holding one is not authority for any other effect.
/// </para>
/// </remarks>
public interface IProviderCredentialLease: IAsyncDisposable
{
    /// <summary>Applies the leased credential to <paramref name="target"/>.</summary>
    /// <param name="target">The request description and header sink for the single outgoing request.</param>
    /// <param name="cancellationToken">A token used to cancel application.</param>
    /// <returns>
    /// <see langword="null"/> after the credential was applied, or a normalized
    /// <see cref="ProviderFailureKind.Authentication"/> failure with a safe message when the credential cannot
    /// authenticate this target (for example an expired token or a credential kind the scheme does not accept). The
    /// failure never contains secret material.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The lease has been disposed.</exception>
    public ValueTask<ProviderFailure?> ApplyAsync(
        IProviderAuthenticationTarget target,
        CancellationToken cancellationToken = default);
}
