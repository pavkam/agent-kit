// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="DocumentStoreKey"/> behavior and contracts.</summary>
public sealed class DocumentStoreKeyTests: StringIdentityConformanceTests<DocumentStoreKey>
{
    /// <inheritdoc/>
    protected override DocumentStoreKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(DocumentStoreKey subject) => subject.Value;
}
