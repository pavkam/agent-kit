// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared approval-store contract against the durable JSON adapter.</summary>
public sealed class JsonApprovalStoreTests: ApprovalStoreConformanceTests<JsonApprovalStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override JsonApprovalStoreConformanceFixture CreateFixture() => new();
}
