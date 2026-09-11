// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class QuestionIdTests: Conformance.GuidIdentityConformanceTests<QuestionId>
{

    /// <inheritdoc/>
    protected override QuestionId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(QuestionId subject) => subject.Value;
}
