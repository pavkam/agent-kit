// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>The single source of every user-facing word about a <see cref="PermissionMode"/>.</summary>
/// <remarks>
/// The menu, the command palette, the <c>/permissions</c> command, the status bar, transcript
/// notices, and the tool reference all read from here, so a mode is named the same way wherever it
/// appears and adding a mode is one edit. Nothing else in the example spells a mode out by hand.
/// </remarks>
internal static class PermissionModeCatalog
{
    /// <summary>Every defined mode in menu order.</summary>
    public static readonly ImmutableArray<PermissionMode> All =
    [
        PermissionMode.AskForChanges,
        PermissionMode.ReadOnly,
        PermissionMode.AutoApproveWorkspaceEdits,
    ];

    /// <summary>Gets the short title used for menu rows, palette rows, and the status bar.</summary>
    /// <param name="mode">The defined mode.</param>
    /// <returns>A two-or-three-word title without a mnemonic marker.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is unknown.</exception>
    public static string Title(PermissionMode mode) => mode switch
    {
        PermissionMode.AskForChanges => "Ask before changes",
        PermissionMode.ReadOnly => "Read-only",
        PermissionMode.AutoApproveWorkspaceEdits => "Auto-approve workspace edits",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "The permission mode is unknown."),
    };

    /// <summary>Gets the title with its Alt-key mnemonic marked for a menu row.</summary>
    /// <param name="mode">The defined mode.</param>
    /// <returns>The <see cref="Title"/> with one <c>&amp;</c> before its access key; the three keys are distinct.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is unknown.</exception>
    public static string MenuLabel(PermissionMode mode) => mode switch
    {
        PermissionMode.AskForChanges => "&Ask before changes",
        PermissionMode.ReadOnly => "&Read-only",
        PermissionMode.AutoApproveWorkspaceEdits => "Auto-approve workspace &edits",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "The permission mode is unknown."),
    };

    /// <summary>Gets the one-sentence explanation of what the mode does to protected tool effects.</summary>
    /// <param name="mode">The defined mode.</param>
    /// <returns>A sentence ending in a period.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is unknown.</exception>
    public static string Description(PermissionMode mode) => mode switch
    {
        PermissionMode.AskForChanges => "Ask before writes, edits, plan changes, and commands.",
        PermissionMode.ReadOnly => "Deny writes, edits, plan changes, and commands.",
        PermissionMode.AutoApproveWorkspaceEdits => "Allow workspace writes and edits; ask before commands and other protected effects.",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "The permission mode is unknown."),
    };

    /// <summary>Gets the argument the <c>/permissions</c> command accepts for the mode.</summary>
    /// <param name="mode">The defined mode.</param>
    /// <returns>A lowercase single word.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is unknown.</exception>
    public static string CommandArgument(PermissionMode mode) => mode switch
    {
        PermissionMode.AskForChanges => "ask",
        PermissionMode.ReadOnly => "readonly",
        PermissionMode.AutoApproveWorkspaceEdits => "auto",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "The permission mode is unknown."),
    };

    /// <summary>Gets the palette identifier for the command that selects the mode.</summary>
    /// <param name="mode">The defined mode.</param>
    /// <returns>A stable dotted identifier.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is unknown.</exception>
    public static string PaletteId(PermissionMode mode) => $"permissions.{CommandArgument(mode)}";

    /// <summary>Resolves the mode a palette command identifier selects.</summary>
    /// <param name="paletteId">The palette command identifier.</param>
    /// <param name="mode">The matched mode when the method returns true.</param>
    /// <returns>True when <paramref name="paletteId"/> is one of the mode commands.</returns>
    public static bool TryParsePaletteId(string? paletteId, out PermissionMode mode)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(paletteId, PaletteId(candidate), StringComparison.Ordinal))
            {
                mode = candidate;
                return true;
            }
        }

        mode = default;
        return false;
    }

    /// <summary>Parses a <c>/permissions</c> argument, accepting the command word or the title.</summary>
    /// <param name="argument">The user-typed argument, possibly empty.</param>
    /// <param name="mode">The matched mode when the method returns true.</param>
    /// <returns>True when <paramref name="argument"/> names exactly one mode.</returns>
    public static bool TryParse(string? argument, out PermissionMode mode)
    {
        var value = argument?.Trim() ?? "";

        foreach (var candidate in All)
        {
            if (string.Equals(value, CommandArgument(candidate), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, Title(candidate), StringComparison.OrdinalIgnoreCase))
            {
                mode = candidate;
                return true;
            }
        }

        mode = default;
        return false;
    }
}
