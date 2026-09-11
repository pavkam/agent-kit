// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionCreationLocationConflictTests: Conformance.SingleMessageLeafConformanceTests<SessionCreationLocationConflict>
{

    /// <inheritdoc/>
    protected override SessionCreationLocationConflict Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionCreationLocationConflict subject) => subject.SafeMessage;
}
