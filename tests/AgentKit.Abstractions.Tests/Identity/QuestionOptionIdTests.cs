// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class QuestionOptionIdTests: Conformance.StringIdentityConformanceTests<QuestionOptionId>
{

    /// <inheritdoc/>
    protected override QuestionOptionId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(QuestionOptionId subject) => subject.Value;
}
