// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed outcomes of activating one captured security-authority binding.</summary>
/// <remarks>
/// A selection result contains no decision or grant. The selected authority must still receive a
/// complete <see cref="SecurityRequest"/> before any protected operation may proceed.
/// </remarks>
public abstract record SecurityAuthoritySelectionResult
{
    /// <summary>Initializes one closed authority-selection outcome.</summary>
    private protected SecurityAuthoritySelectionResult()
    {
    }
}
