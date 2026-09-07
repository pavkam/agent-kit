// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Confirms that one committed artifact version is absent.</summary>
/// <param name="AlreadyAbsent">Whether content was already absent when deletion executed.</param>
public sealed record ArtifactDeleted(bool AlreadyAbsent): ArtifactDeleteResult;
