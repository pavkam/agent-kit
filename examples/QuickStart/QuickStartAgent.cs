// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace QuickStart;

/// <summary>Configures the quick-start agent: one OpenAI model, local development defaults, one instruction.</summary>
internal static class QuickStartAgent
{
    /// <summary>Creates the configured engine builder; call <c>Build()</c> to get the engine.</summary>
    /// <param name="apiKey">The OpenAI API key; credentials are never read implicitly.</param>
    /// <returns>A builder whose <c>Services</c> can still be adjusted (the tests substitute the HTTP client).</returns>
    public static AgentEngineBuilder CreateBuilder(string apiKey) =>
        AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()        // in-memory state, allow-all policy, local identity: named so it is never shipped by accident
            .UseOpenAI(apiKey, "gpt-4o-mini")     // adapter, credential, and catalog descriptor in one call
            .WithInstructions("You are a concise assistant.");
}
