// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Authors the keyboard reference shown by <c>/keys</c> and the Help menu.</summary>
internal static class KeyboardShortcutsReference
{
    /// <summary>The dialog title.</summary>
    public const string Title = "Keyboard Shortcuts";

    /// <summary>Gets the authored keyboard reference before Markdown projection.</summary>
    internal static string Markdown =>
        """
        # Keyboard shortcuts

        > Keys are grouped by where they act. When a dialog or palette is open, it owns the keyboard until you close it.

        ## Write and send

        - **`Enter` — Send message.** Submits the composer or answers the active question.
        - **`Shift+Enter` — New line.** Inserts a line break without sending.
        - **`Ctrl+P` / `Ctrl+N` — Prompt history.** Recalls the previous or next submitted prompt.

        ## Command palette

        - **`/` or `Ctrl+K` — Open commands.** Search sessions, navigation, configuration, permissions, help, and application actions.
        - **`Ctrl+Shift+P` — Open commands.** Alternate palette chord.
        - **`Up` / `Down` — Move selection.** Chooses the previous or next matching command.
        - **`Enter` — Run command.** Invokes the highlighted palette action.
        - **`Esc` — Dismiss.** Closes the palette and restores composer focus.

        ## Transcript navigation

        - **`Page Up` / `Page Down` — Move one page.** Scrolls toward older or newer messages.
        - **`Ctrl+End` — Follow latest.** Jumps to the newest message and resumes automatic follow.
        - **`Esc` — Leave transcript.** Clears transcript text selection and returns to the composer.

        ## Approvals and questions

        - **`Enter` or `Y` — Allow.** Approves the exact pending tool request.
        - **`Esc` or `N` — Deny.** Rejects the pending request without running its effect.
        - **`1`…`9` then `Enter` — Answer.** Chooses a numbered question option; free text is available when requested.

        ## Windows and application

        - **`Alt+S/V/A/P/H` — Open a menu.** Activates Session, View, Agent, Permissions, or Help.
        - **`Tab` / `Shift+Tab` — Move focus.** Traverses controls in dialogs and menus.
        - **`Esc` — Close or stop.** Closes the active dialog; during a run it cancels the current turn.
        - **`Ctrl+Q` — Quit.** Exits CodingAgent.

        ---

        **Tip:** Press `Ctrl+K`, type a few words, then press `Enter`. It is usually faster than hunting through menus.
        """;
}
