// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Architecture.Tests;

using System.Runtime.CompilerServices;

using Microsoft.Build.Locator;

/// <summary>Initializes process-wide architecture-test dependencies before tests can run concurrently.</summary>
internal static class ArchitectureTestBootstrap
{
    /// <summary>Registers the installed SDK's MSBuild assemblies exactly once during module initialization.</summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            _ = MSBuildLocator.RegisterDefaults();
        }
    }
}
