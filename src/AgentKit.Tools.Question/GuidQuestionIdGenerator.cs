// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Question;

/// <summary>Generates cryptographically unpredictable question identities for the default registration.</summary>
internal sealed class GuidQuestionIdGenerator: IIdentifierGenerator<QuestionId>
{
    /// <inheritdoc/>
    public QuestionId Create() => new(Guid.NewGuid());
}
