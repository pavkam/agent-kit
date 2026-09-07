// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

internal static class CompatibilityTestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        DiffRunner.Disabled = true;
        if (!MSBuildLocator.IsRegistered)
        {
            _ = MSBuildLocator.RegisterDefaults();
        }
    }
}
