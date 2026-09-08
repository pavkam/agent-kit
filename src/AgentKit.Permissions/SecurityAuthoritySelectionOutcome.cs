// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Classifies the bounded terminal result of one captured-authority selection.</summary>
internal enum SecurityAuthoritySelectionOutcome
{
    /// <summary>The exact captured authority key resolved successfully.</summary>
    Selected,

    /// <summary>The exact captured authority key has no active binding.</summary>
    Unavailable,

    /// <summary>The caller cancelled selection before it completed.</summary>
    Cancelled,

    /// <summary>Selection faulted unexpectedly.</summary>
    Failed,
}
