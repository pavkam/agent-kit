// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ContentHashTests: Conformance.StringIdentityConformanceTests<ContentHash>
{

    /// <inheritdoc/>
    protected override ContentHash Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ContentHash subject) => subject.Value;
}
