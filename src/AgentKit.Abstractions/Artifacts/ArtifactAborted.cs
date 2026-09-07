// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Confirms that unpublished content is absent and cannot be finalized.</summary>
public sealed record ArtifactAborted(bool AlreadyAbsent): ArtifactAbortResult;
