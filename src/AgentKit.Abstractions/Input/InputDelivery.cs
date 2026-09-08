// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines when admitted conversational input becomes eligible for promotion.</summary>
public enum InputDelivery
{
    /// <summary>Promotes at the next safe boundary of the current run.</summary>
    Steer,
    /// <summary>Promotes only when current work would otherwise finish.</summary>
    FollowUp,
}
