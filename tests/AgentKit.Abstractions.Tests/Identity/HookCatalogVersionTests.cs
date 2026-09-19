// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class HookCatalogVersionTests: Conformance.StringIdentityConformanceTests<HookCatalogVersion>
{
    /// <inheritdoc/>
    protected override HookCatalogVersion Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(HookCatalogVersion subject) => subject.Value;
}
