// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports newly captured authorization evidence for an exact scope and identity.</summary>
public sealed record SecurityAuthorizationCaptured: SecurityAuthorizationCaptureResult
{
    /// <summary>Initializes a successful authorization capture.</summary><param name="authorization">The non-null freshly captured authorization evidence.</param><exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception>
    public SecurityAuthorizationCaptured(SecurityAuthorizationContext authorization) { ArgumentNullException.ThrowIfNull(authorization); Authorization = authorization; }
    /// <summary>Gets the fresh captured authorization.</summary><value>Non-null immutable evidence bound to the requested scope and identity.</value>
    public SecurityAuthorizationContext Authorization { get; }
}
