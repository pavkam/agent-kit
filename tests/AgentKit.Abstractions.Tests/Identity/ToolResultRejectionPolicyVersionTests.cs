// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ToolResultRejectionPolicyVersionTests: Conformance.LongIdentityConformanceTests<ToolResultRejectionPolicyVersion>
{

    /// <inheritdoc/>
    protected override ToolResultRejectionPolicyVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(ToolResultRejectionPolicyVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
