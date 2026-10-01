// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Confirms that a store holds no readable bytes for the exact committed version.</summary>
/// <param name="AlreadyAbsent">Whether the version was already deleted when the deletion executed.</param>
public sealed record ArtifactStoreDeleted(bool AlreadyAbsent): ArtifactStoreDeleteResult;
