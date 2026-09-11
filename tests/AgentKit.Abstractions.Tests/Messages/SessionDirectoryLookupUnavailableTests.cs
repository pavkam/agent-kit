// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionDirectoryLookupUnavailableTests: Conformance.SingleMessageLeafConformanceTests<SessionDirectoryLookupUnavailable>
{

    /// <inheritdoc/>
    protected override SessionDirectoryLookupUnavailable Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionDirectoryLookupUnavailable subject) => subject.SafeMessage;
}
