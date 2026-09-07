// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted;

/// <summary>Declares one identified deterministic language-query result and post-admission delay.</summary>
public sealed record ScriptedLanguageScenario
{
    /// <summary>Initializes one scripted query scenario.</summary>
    /// <param name="queryId">The exact query identity selecting the scenario.</param>
    /// <param name="result">The declared terminal result.</param>
    /// <param name="delayAfterAdmission">The non-negative injected-time delay after grant consumption.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The identity or delay is invalid.</exception>
    public ScriptedLanguageScenario(
        LanguageQueryId queryId,
        LanguageQueryResult result,
        TimeSpan delayAfterAdmission)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(queryId.Value, Guid.Empty, nameof(queryId));
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfLessThan(delayAfterAdmission, TimeSpan.Zero);
        QueryId = queryId;
        Result = result;
        DelayAfterAdmission = delayAfterAdmission;
    }

    /// <summary>Gets the exact query identity selecting the scenario.</summary>
    public LanguageQueryId QueryId { get; }
    /// <summary>Gets the declared terminal result.</summary>
    public LanguageQueryResult Result { get; }
    /// <summary>Gets the injected-time delay after grant consumption.</summary>
    public TimeSpan DelayAfterAdmission { get; }
}
