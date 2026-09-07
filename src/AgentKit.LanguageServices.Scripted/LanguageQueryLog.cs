// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted;

/// <summary>Defines allocation-efficient language-query events without paths, source text, or result content.</summary>
internal static partial class LanguageQueryLog
{
    /// <summary>Records one typed terminal language-query status.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="languageQueryId">The causal language-query identity.</param>
    /// <param name="languageQueryKind">The bounded query kind.</param>
    /// <param name="outcome">The terminal query status.</param>
    [LoggerMessage(15000, LogLevel.Debug, "Language query {LanguageQueryId} of kind {LanguageQueryKind} completed with outcome {Outcome}.")]
    internal static partial void Completed(
        ILogger logger,
        LanguageQueryId languageQueryId,
        LanguageQueryKind languageQueryKind,
        LanguageQueryStatus outcome);

    /// <summary>Records an unexpected language-query exception type without exception content.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="languageQueryId">The causal language-query identity.</param>
    /// <param name="languageQueryKind">The bounded query kind.</param>
    /// <param name="errorType">The stable exception type name.</param>
    [LoggerMessage(15001, LogLevel.Error, "Language query {LanguageQueryId} of kind {LanguageQueryKind} failed with error type {ErrorType}.")]
    internal static partial void Failed(
        ILogger logger,
        LanguageQueryId languageQueryId,
        LanguageQueryKind languageQueryKind,
        string errorType);
}
