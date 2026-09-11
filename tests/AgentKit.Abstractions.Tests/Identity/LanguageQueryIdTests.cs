// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class LanguageQueryIdTests: Conformance.GuidIdentityConformanceTests<LanguageQueryId>
{

    /// <inheritdoc/>
    protected override LanguageQueryId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(LanguageQueryId subject) => subject.Value;
}
