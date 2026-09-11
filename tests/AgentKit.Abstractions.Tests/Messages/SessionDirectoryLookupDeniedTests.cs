// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionDirectoryLookupDeniedTests: Conformance.SingleMessageLeafConformanceTests<SessionDirectoryLookupDenied>
{

    /// <inheritdoc/>
    protected override SessionDirectoryLookupDenied Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionDirectoryLookupDenied subject) => subject.SafeMessage;
}
