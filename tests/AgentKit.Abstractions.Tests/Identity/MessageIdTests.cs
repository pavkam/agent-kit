// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class MessageIdTests: Conformance.GuidIdentityConformanceTests<MessageId>
{

    /// <inheritdoc/>
    protected override MessageId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(MessageId subject) => subject.Value;
}
