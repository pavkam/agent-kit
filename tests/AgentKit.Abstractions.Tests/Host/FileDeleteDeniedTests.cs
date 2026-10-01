// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileDeleteDenied behavior and contracts.</summary>
public sealed class FileDeleteDeniedTests: Conformance.SingleMessageLeafConformanceTests<FileDeleteDenied>
{
    /// <inheritdoc/>
    protected override FileDeleteDenied Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(FileDeleteDenied subject) => subject.SafeMessage;
}
