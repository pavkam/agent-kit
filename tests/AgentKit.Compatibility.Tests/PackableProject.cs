// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

public sealed record PackableProject(string ProjectPath, string AssemblyName)
{
    public override string ToString() => AssemblyName;
}
