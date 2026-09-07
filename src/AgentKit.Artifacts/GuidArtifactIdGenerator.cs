// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit.Artifacts;
/// <summary>Generates unpredictable logical artifact identities.</summary>
internal sealed class GuidArtifactIdGenerator: IIdentifierGenerator<ArtifactId>
{
    /// <inheritdoc/>
    public ArtifactId Create() => new(Guid.NewGuid());
}
