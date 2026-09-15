// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace QuickStart;

/// <summary>Builds the quick-start agent: one OpenAI model, local development defaults, one instruction.</summary>
internal static class QuickStartAgent
{
    /// <summary>Creates the agent.</summary>
    /// <param name="apiKey">The OpenAI API key; credentials are never read implicitly.</param>
    /// <param name="configure">An optional hook over the service collection, used by tests to substitute the HTTP client.</param>
    /// <returns>A ready agent that owns its composition.</returns>
    public static SimpleAgent Create(string apiKey, Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? configure = null)
    {
        var builder = SimpleAgentBuilder.Create()
            .UseLocalDevelopmentDefaults()        // in-memory state, allow-all policy, local identity: named so it is never shipped by accident
            .UseOpenAI(apiKey, "gpt-4o-mini")     // adapter, credential, and catalog descriptor in one call
            .WithInstructions("You are a concise assistant.");

        configure?.Invoke(builder.Services);
        return builder.Build();
    }
}
