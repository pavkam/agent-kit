// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class IdempotencyKeyTests: Conformance.StringIdentityConformanceTests<IdempotencyKey>
{

    /// <inheritdoc/>
    protected override IdempotencyKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(IdempotencyKey subject) => subject.Value;
}
