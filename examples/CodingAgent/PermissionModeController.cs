// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Owns the example's process-local permission mode so the UI and security policy observe one atomic value.</summary>
internal sealed class PermissionModeController
{
    private int _mode = (int) PermissionMode.AskForChanges;

    /// <summary>Gets the mode applied when the next protected effect reaches the security authority.</summary>
    public PermissionMode Mode => (PermissionMode) Volatile.Read(ref _mode);

    /// <summary>Changes the mode used by later policy decisions, including effects waiting behind an active call.</summary>
    /// <param name="mode">The supported permission mode to activate.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined mode.</exception>
    public void Set(PermissionMode mode)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(Enum.IsDefined(mode), true, nameof(mode));
        Volatile.Write(ref _mode, (int) mode);
    }

    /// <summary>Gets the current mode's title from <see cref="PermissionModeCatalog"/>.</summary>
    /// <returns>The same title the menu, palette, and status bar show for the mode.</returns>
    public string Label() => PermissionModeCatalog.Title(Mode);
}
