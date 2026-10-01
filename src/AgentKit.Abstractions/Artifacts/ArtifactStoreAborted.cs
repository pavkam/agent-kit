// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Confirms that a store holds no publishable staging for the preparation.</summary>
/// <param name="AlreadyAbsent">Whether the staging was already aborted, expired, or deleted when the abort executed.</param>
public sealed record ArtifactStoreAborted(bool AlreadyAbsent): ArtifactStoreAbortResult;
