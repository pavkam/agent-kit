// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A credential source released one disposable credential lease.</summary>
/// <remarks>The receiver owns <see cref="Credential"/> and disposes it as soon as the credential has been applied.</remarks>
public sealed record ProviderCredentialResolved: ProviderCredentialResolutionResult
{
    /// <summary>Initializes a resolved result.</summary>
    /// <param name="credential">The owned lease holding opaque secret material.</param>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is null.</exception>
    public ProviderCredentialResolved(IProviderCredentialLease credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        Credential = credential;
    }

    /// <summary>Gets the owned credential lease.</summary>
    /// <value>A lease the receiver must dispose exactly once.</value>
    public IProviderCredentialLease Credential { get; }

    /// <summary>Returns a redacted textual form that never renders the lease.</summary>
    /// <returns>The fixed type name.</returns>
    public override string ToString() => nameof(ProviderCredentialResolved);

    /// <summary>Appends no members so the lease is never rendered.</summary>
    /// <param name="builder">The builder receiving member text.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    protected override bool PrintMembers(System.Text.StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return false;
    }
}
