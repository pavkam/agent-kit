// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Describes one slash command the chat prompt recognizes instead of sending it to the agent.</summary>
/// <param name="Name">The exact leading token used by command dispatch.</param>
/// <param name="Syntax">The user-facing invocation, including any required argument placeholder.</param>
/// <param name="Section">The command-reference section that owns the command.</param>
/// <param name="Description">A concise statement of the command's effect.</param>
/// <param name="Details">Operational guidance that helps the user invoke the command correctly.</param>
internal sealed record SlashCommand(
    string Name,
    string Syntax,
    string Section,
    string Description,
    string Details);
