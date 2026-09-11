// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class RunPolicyVersionTests: Conformance.LongIdentityConformanceTests<RunPolicyVersion>
{

    /// <inheritdoc/>
    protected override RunPolicyVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(RunPolicyVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
