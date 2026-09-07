// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The portable policy a <see cref="LlmToolChoice"/> applies to tool-call
/// selection for one request.
/// </summary>
public enum LlmToolChoiceMode
{
    /// <summary>The model decides whether to call a tool and which one.</summary>
    Auto,

    /// <summary>The model must not call any tool.</summary>
    None,

    /// <summary>The model must call at least one of the advertised tools.</summary>
    Required,

    /// <summary>The model must call the specific tool named by <see cref="LlmToolChoice.ForcedToolName"/>.</summary>
    Named
}
