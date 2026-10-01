// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Carries one safe, content-free diagnostic about how a case repetition was run or recorded.</summary>
public sealed record EvaluationDiagnostic
{
    /// <summary>Initializes a validated diagnostic.</summary>
    /// <param name="code">The non-blank stable machine-readable code.</param>
    /// <param name="safeMessage">The non-blank explanation that never contains prompts, model output, tool data, or secrets.</param>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> or <paramref name="safeMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="safeMessage"/> is blank.</exception>
    public EvaluationDiagnostic(string code, string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Code = code;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the stable machine-readable code.</summary>
    public string Code { get; }

    /// <summary>Gets the safe explanation.</summary>
    public string SafeMessage { get; }
}
