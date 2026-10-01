// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Reports the backend selected for a directory.</summary>
/// <param name="Backend">The configured backend key.</param>
/// <param name="Store">The explicitly registered store for the key.</param>
internal sealed record ArtifactStoreSelected(ArtifactBackendKey Backend, IArtifactStore Store): ArtifactStoreSelectionResult;
