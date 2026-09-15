// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using AgentKit;

/// <summary>Supplies deterministic terminal question behavior to channel adapter tests.</summary>
internal sealed class DelegateHumanQuestionPrompt(
    Func<HumanQuestionPrompt, CancellationToken, ValueTask<HumanQuestionSelection>> callback): IHumanQuestionPrompt
{
    public ValueTask<HumanQuestionSelection> AskAsync(
        HumanQuestionPrompt prompt,
        CancellationToken cancellationToken = default) => callback(prompt, cancellationToken);
}
