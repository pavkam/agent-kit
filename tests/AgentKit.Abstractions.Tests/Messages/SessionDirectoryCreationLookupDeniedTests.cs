// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionDirectoryCreationLookupDeniedTests: Conformance.SingleMessageLeafConformanceTests<SessionDirectoryCreationLookupDenied>
{

    /// <inheritdoc/>
    protected override SessionDirectoryCreationLookupDenied Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionDirectoryCreationLookupDenied subject) => subject.SafeMessage;
}
