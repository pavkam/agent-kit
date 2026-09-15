// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Reports that the headless smoke-test path has no authenticated interactive respondent.</summary>
internal sealed class UnavailableHumanQuestionPrompt: IHumanQuestionPrompt
{
    /// <inheritdoc/>
    public ValueTask<HumanQuestionSelection> AskAsync(
        HumanQuestionPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("No interactive human-question channel is attached.");
    }
}
