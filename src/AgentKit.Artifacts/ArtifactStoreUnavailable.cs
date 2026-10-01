// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Reports that no registered backend serves the requested directory or key.</summary>
/// <param name="Failure">The typed, content-free failure.</param>
internal sealed record ArtifactStoreUnavailable(ArtifactFailure Failure): ArtifactStoreSelectionResult;
