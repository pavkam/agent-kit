// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Identifies why a run-local subscription could not be registered.</summary>
internal enum RunEventSubscriptionRejection
{
    /// <summary>The configured simultaneous subscription bound has been reached.</summary>
    CapacityReached,
    /// <summary>The hub completed or was disposed and accepts no further recipients.</summary>
    HubClosed,
}
