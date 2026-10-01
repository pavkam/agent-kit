// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Is the planned outcome of one intent-store operation, evaluated before anything is persisted or applied.</summary>
/// <remarks>A durable adapter persists <see cref="Persist"/> first and only then applies the decision to its projection, so an acknowledged result is always durable and a persistence failure leaves the projection unchanged.</remarks>
/// <param name="Result">The result the operation reports once any <paramref name="Persist"/> value is durable.</param>
/// <param name="Persist">The intent to write and project, or <see langword="null"/> when the operation changes nothing.</param>
internal readonly record struct ArtifactReferenceCommitIntentDecision(
    ArtifactReferenceCommitIntentResult Result,
    ArtifactReferenceCommitIntent? Persist);
