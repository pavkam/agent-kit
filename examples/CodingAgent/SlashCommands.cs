// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Provides the fixed command catalog shared by dispatch, completion, and formatted help.</summary>
internal static class SlashCommands
{
    /// <summary>Gets every recognized command in the order used by command discovery and help.</summary>
    public static readonly ImmutableArray<SlashCommand> All =
    [
        new("/help", "/help", "Help and application", "Open this formatted command reference.",
            "The reference is selectable and scrollable; press `Esc` to return to the composer."),
        new("/clear", "/clear", "Sessions and transcript", "Clear the visible transcript.",
            "The durable conversation continues, so later agent turns still receive its history."),
        new("/new", "/new", "Sessions and transcript", "Start a brand-new conversation.",
            "This releases the current runtime and begins with no conversation history."),
        new("/sessions", "/sessions", "Sessions and transcript", "List recent durable sessions for this workspace.",
            "Use an identifier from the result with `/resume <session-id>`."),
        new("/resume", "/resume <session-id>", "Sessions and transcript", "Open a durable conversation by identifier.",
            "The stored transcript and reported usage are restored. Run `/sessions` to find an identifier."),
        new("/model", "/model", "Agent and workspace", "Open Agent Configuration.",
            "Choose Terra, Sol, or Astra, set reasoning effort, and adjust the tool-call turn limit. Saving starts a fresh session."),
        new("/workspace", "/workspace", "Agent and workspace", "Open Configure Workspace.",
            "Inspect the writable root and choose which host-declared toolchain roots are exposed read-only."),
        new("/status", "/status", "Agent and workspace", "Inspect live agent configuration and run state.",
            "Opens Agent Configuration with the current model, reasoning effort, and turn limit."),
        new("/tools", "/tools", "Agent and workspace", "Inspect model-facing tools and sandbox state.",
            "The reference ends with the permission policy that applies to the next protected tool call."),
        new("/keys", "/keys", "Help and application", "Open the formatted keyboard-shortcut reference.",
            "The reference covers the composer, palette, transcript, approvals, questions, and application windows."),
        new("/permissions", "/permissions [ask|readonly|auto]", "Agent and workspace", "Show or change the live permission mode.",
            "Without an argument it lists the modes and marks the current one; with one it switches immediately, exactly like the Permissions menu. The session keeps running."),
        new("/quit", "/quit", "Help and application", "Exit CodingAgent.",
            "`Ctrl+Q` is the keyboard equivalent."),
    ];
}
