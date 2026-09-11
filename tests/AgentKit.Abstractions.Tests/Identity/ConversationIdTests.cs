// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ConversationIdTests: Conformance.GuidIdentityConformanceTests<ConversationId>
{

    /// <inheritdoc/>
    protected override ConversationId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(ConversationId subject) => subject.Value;
}
