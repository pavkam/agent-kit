// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted;

/// <summary>Configures deterministic language-query scenarios.</summary>
public sealed class ScriptedLanguageOptions
{
    /// <summary>Gets the exact operation scenarios returned by the scripted provider.</summary>
    public List<ScriptedLanguageScenario> Scenarios { get; } = [];
}
