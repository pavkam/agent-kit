// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared grant-store contract against the durable JSON adapter.</summary>
public sealed class JsonSecurityGrantStoreTests
    : SecurityGrantStoreConformanceTests<JsonSecurityGrantStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override JsonSecurityGrantStoreConformanceFixture CreateFixture() => new();
}
