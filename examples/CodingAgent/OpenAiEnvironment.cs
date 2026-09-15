// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Reads the OpenAI credential from the process environment.</summary>
internal static class OpenAiEnvironment
{
    /// <summary>The environment variable this example reads its OpenAI API key from.</summary>
    public const string ApiKeyVariable = "OPENAI_API_KEY";

    /// <summary>Gets the configured OpenAI model id, defaulting to a small, fast model.</summary>
    public const string ModelVariable = "CODING_AGENT_MODEL";

    private const string _defaultModelId = "gpt-5.6-terra";

    /// <summary>Reads the required API key.</summary>
    /// <exception cref="InvalidOperationException"><see cref="ApiKeyVariable"/> is unset or blank.</exception>
    public static string RequireApiKey()
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        return string.IsNullOrWhiteSpace(apiKey)
            ? throw new InvalidOperationException(
                $"Set the {ApiKeyVariable} environment variable before running CodingAgent.")
            : apiKey;
    }

    /// <summary>Reads the configured model id, or a default when unset.</summary>
    public static string ModelId()
    {
        var configured = Environment.GetEnvironmentVariable(ModelVariable);
        return string.IsNullOrWhiteSpace(configured) ? _defaultModelId : configured;
    }
}
