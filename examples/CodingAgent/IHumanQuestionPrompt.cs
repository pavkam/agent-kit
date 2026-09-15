// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Presents an authority-free human question in the local terminal.</summary>
internal interface IHumanQuestionPrompt
{
    /// <summary>Presents one bounded prompt and waits for a single terminal selection.</summary>
    /// <param name="prompt">The grant-free question projection to display.</param>
    /// <param name="cancellationToken">Cancels the pending display and wait.</param>
    /// <returns>The selected option and optional free text.</returns>
    public ValueTask<HumanQuestionSelection> AskAsync(
        HumanQuestionPrompt prompt,
        CancellationToken cancellationToken = default);
}
