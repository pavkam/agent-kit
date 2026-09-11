// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SecurityProfilePublicationUnavailableTests: Conformance.SingleMessageLeafConformanceTests<SecurityProfilePublicationUnavailable>
{

    /// <inheritdoc/>
    protected override SecurityProfilePublicationUnavailable Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SecurityProfilePublicationUnavailable subject) => subject.SafeReason;
}
