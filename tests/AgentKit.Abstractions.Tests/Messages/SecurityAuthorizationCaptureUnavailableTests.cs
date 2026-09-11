// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SecurityAuthorizationCaptureUnavailableTests: Conformance.SingleMessageLeafConformanceTests<SecurityAuthorizationCaptureUnavailable>
{

    /// <inheritdoc/>
    protected override SecurityAuthorizationCaptureUnavailable Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SecurityAuthorizationCaptureUnavailable subject) => subject.SafeReason;
}
