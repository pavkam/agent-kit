// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="DocumentVersion"/> behavior and contracts.</summary>
public sealed class DocumentVersionTests: StringIdentityConformanceTests<DocumentVersion>
{
    /// <inheritdoc/>
    protected override DocumentVersion Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(DocumentVersion subject) => subject.Value;
}
