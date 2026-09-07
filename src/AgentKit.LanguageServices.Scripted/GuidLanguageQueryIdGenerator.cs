// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted;

/// <summary>Creates collision-resistant language-query identities for ordinary live composition.</summary>
/// <remarks>Deterministic tests and replay replace this default generator.</remarks>
internal sealed class GuidLanguageQueryIdGenerator: IIdentifierGenerator<LanguageQueryId>
{
    /// <inheritdoc/>
    public LanguageQueryId Create() => new(Guid.NewGuid());
}
