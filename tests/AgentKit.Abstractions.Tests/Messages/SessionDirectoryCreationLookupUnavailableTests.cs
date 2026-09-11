// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionDirectoryCreationLookupUnavailableTests: Conformance.SingleMessageLeafConformanceTests<SessionDirectoryCreationLookupUnavailable>
{

    /// <inheritdoc/>
    protected override SessionDirectoryCreationLookupUnavailable Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionDirectoryCreationLookupUnavailable subject) => subject.SafeMessage;
}
