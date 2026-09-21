// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Builds context assemblers for loop unit tests.</summary>
internal static class ContextAssemblerTestSupport
{
    /// <summary>Resolves the default keyed assembler from a minimal service provider.</summary>
    internal static IContextAssembler CreateDefault() => CreateServiceProvider().GetRequiredService<IContextAssembler>();

    internal static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentContext();
        return services.BuildServiceProvider();
    }
}
