// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Creates random evaluation run identities for production composition.</summary>
/// <remarks>The generator is stateless and thread-safe. Deterministic tests and replay replace the registered <see cref="IIdentifierGenerator{TIdentifier}"/> with a controllable sequence.</remarks>
internal sealed class GuidEvaluationRunIdGenerator: IIdentifierGenerator<EvaluationRunId>
{
    /// <inheritdoc/>
    public EvaluationRunId Create() => new(Guid.NewGuid());
}
