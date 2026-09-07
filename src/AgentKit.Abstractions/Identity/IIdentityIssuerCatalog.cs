// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves one immutable, keyed issuer registration without relying on registration order.</summary>
public interface IIdentityIssuerCatalog
{
    /// <summary>Finds the issuer registered under a trusted assertion's issuer key.</summary>
    /// <param name="issuerId">The stable issuer key.</param>
    /// <returns>The configured issuer, or <see langword="null"/> when no mapping exists.</returns>
    public IIdentityIssuer? Find(IdentityIssuerId issuerId);
}
