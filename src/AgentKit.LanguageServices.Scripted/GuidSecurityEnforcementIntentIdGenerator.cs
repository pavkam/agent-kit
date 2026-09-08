// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted;

/// <summary>Creates unique enforcement-intent identities for default scripted language queries.</summary>
internal sealed class GuidSecurityEnforcementIntentIdGenerator: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <summary>Creates a fresh identity that binds one language observation attempt.</summary>
    /// <returns>A non-default identity containing no query text, path, or result content.</returns>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
