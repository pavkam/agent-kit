// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Creates distinct random <see cref="DocumentId"/> values from the container's replaceable identifier contract.</summary>
/// <remarks>Deterministic creation in tests uses an injected replacement of <see cref="IIdentifierGenerator{TIdentifier}"/>; nothing in the runtime calls <see cref="Guid.NewGuid"/> outside this default.</remarks>
internal sealed class GuidDocumentIdGenerator: IIdentifierGenerator<DocumentId>
{
    /// <summary>Creates a non-default identity distinct from every other.</summary>
    /// <returns>A new identity.</returns>
    public DocumentId Create() => new(Guid.NewGuid());
}
