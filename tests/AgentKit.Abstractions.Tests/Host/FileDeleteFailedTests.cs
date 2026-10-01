// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileDeleteFailed behavior and contracts.</summary>
public sealed class FileDeleteFailedTests: Conformance.SingleMessageLeafConformanceTests<FileDeleteFailed>
{
    /// <inheritdoc/>
    protected override FileDeleteFailed Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(FileDeleteFailed subject) => subject.SafeMessage;
}
