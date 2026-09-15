// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One ordered, provisional observation produced while an agent run is executing.</summary>
/// <remarks>These events do not replace committed session records or the terminal <see cref="AgentLoopResult"/>.</remarks>
public abstract record AgentRunEvent
{
    private protected AgentRunEvent()
    {
    }
}
