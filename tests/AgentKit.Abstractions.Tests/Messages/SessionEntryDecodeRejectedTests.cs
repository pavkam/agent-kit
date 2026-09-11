// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionEntryDecodeRejectedTests: Conformance.SingleMessageLeafConformanceTests<SessionEntryDecodeRejected>
{

    /// <inheritdoc/>
    protected override SessionEntryDecodeRejected Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionEntryDecodeRejected subject) => subject.Reason;
}
