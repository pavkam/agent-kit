// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using AgentKit.Hooks;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the default hook kernel for compositions that must satisfy engine validation.</summary>
public static class HookCompositionTestSupport
{
    /// <summary>Registers the first-party hook kernel when it is not already present.</summary>
    /// <param name="services">The composition under test.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static void TryAddDefaultHookKernel(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(static descriptor =>
                !descriptor.IsKeyedService && descriptor.ServiceType == typeof(IHookDispatcher)))
        {
            return;
        }

        _ = services.AddAgentHooks();
    }
}
