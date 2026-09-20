// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared run-coordinator contract against the JSON session store.</summary>
public sealed class JsonSessionRunCoordinatorConformanceTests
    : SessionRunCoordinatorConformanceTests<JsonSessionRunCoordinatorConformanceFixture>
{
    /// <inheritdoc/>
    protected override JsonSessionRunCoordinatorConformanceFixture CreateFixture() => new();
}
