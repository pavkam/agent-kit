// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionDirectoryWriteUnavailableTests: Conformance.SingleMessageLeafConformanceTests<SessionDirectoryWriteUnavailable>
{

    /// <inheritdoc/>
    protected override SessionDirectoryWriteUnavailable Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionDirectoryWriteUnavailable subject) => subject.SafeMessage;
}
