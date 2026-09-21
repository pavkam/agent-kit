// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Builds context services for unit tests.</summary>
internal static class ContextTestSupport
{
    /// <summary>Resolves the default keyed assembler from a minimal service provider.</summary>
    internal static DefaultContextAssembler CreateAssembler(Action<AgentContextOptions>? configure = null)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentContext(configure);
        return (DefaultContextAssembler) services.BuildServiceProvider().GetRequiredService<IContextAssembler>();
    }

    /// <summary>Resolves the default instruction resolver from a minimal service provider.</summary>
    internal static IInstructionResolver CreateResolver()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentContext();
        return services.BuildServiceProvider().GetRequiredService<IInstructionResolver>();
    }

    /// <summary>Resolves the default keyed assembler with a substituted logger.</summary>
    internal static DefaultContextAssembler CreateAssembler(ILogger<DefaultContextAssembler> logger, Action<AgentContextOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var services = new ServiceCollection();
        _ = services.AddSingleton(logger);
        _ = services.AddAgentContext(configure);
        return (DefaultContextAssembler) services.BuildServiceProvider().GetRequiredService<IContextAssembler>();
    }
}
