// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies why explicit store selection could not safely produce a store.</summary>
public enum SessionStoreSelectionRejectionReason
{
    /// <summary>The requested pinned or default store key is not composed.</summary>
    MissingStoreKey,
    /// <summary>The selected store's durability cannot satisfy the requested session behavior.</summary>
    IncompatibleDurability,
    /// <summary>The selected store lacks a required declared capability.</summary>
    IncompatibleCapabilities,
    /// <summary>The context, directory location, or compiled configuration is inconsistent.</summary>
    IdentityAddressOrConfigurationMismatch,
}
