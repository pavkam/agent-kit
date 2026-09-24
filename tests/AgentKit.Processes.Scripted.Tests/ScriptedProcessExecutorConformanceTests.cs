// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted.Tests;

using AgentKit.Conformance;

/// <summary>Runs shared process executor conformance scenarios against the scripted adapter.</summary>
public sealed class ScriptedProcessExecutorConformanceTests: ProcessExecutorConformanceTests<ScriptedProcessExecutorConformanceFixture>
{
    /// <inheritdoc/>
    protected override ScriptedProcessExecutorConformanceFixture CreateFixture() => new();
}
