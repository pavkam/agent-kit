// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

using AgentKit.Conformance;

/// <summary>Runs the portable schema-engine contract against the publicly composed structural engine.</summary>
public sealed class StructuralSchemaConformanceTests: OutputSchemaEngineConformanceTests<StructuralSchemaConformanceFixture>
{
    /// <inheritdoc/>
    protected override StructuralSchemaConformanceFixture CreateFixture() => new();
}
