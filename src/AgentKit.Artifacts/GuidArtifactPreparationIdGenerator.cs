// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit.Artifacts;
/// <summary>Generates unpredictable artifact staging identities.</summary>
internal sealed class GuidArtifactPreparationIdGenerator: IIdentifierGenerator<ArtifactPreparationId>
{
    /// <inheritdoc/>
    public ArtifactPreparationId Create() => new(Guid.NewGuid());
}
