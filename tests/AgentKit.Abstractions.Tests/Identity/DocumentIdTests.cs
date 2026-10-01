// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="DocumentId"/> behavior and contracts.</summary>
public sealed class DocumentIdTests: GuidIdentityConformanceTests<DocumentId>
{
    /// <inheritdoc/>
    protected override DocumentId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(DocumentId subject) => subject.Value;
}
