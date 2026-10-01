// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed outcome of asking a credential source to resolve one credential lease.</summary>
/// <remarks>The concrete kinds are <see cref="ProviderCredentialResolved"/> and <see cref="ProviderCredentialUnavailable"/>.</remarks>
public abstract record ProviderCredentialResolutionResult
{
    /// <summary>Prevents declaration of kinds outside this assembly.</summary>
    private protected ProviderCredentialResolutionResult()
    {
    }
}
