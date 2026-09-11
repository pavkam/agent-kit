// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class NetworkResolutionDeniedTests: Conformance.SingleMessageLeafConformanceTests<NetworkResolutionDenied>
{

    /// <inheritdoc/>
    protected override NetworkResolutionDenied Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(NetworkResolutionDenied subject) => subject.SafeMessage;
}
