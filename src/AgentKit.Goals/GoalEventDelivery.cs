// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Declares how a failure to deliver an event to one sink is treated.</summary>
public enum GoalEventDelivery
{
    /// <summary>Delivery is best effort: a missing or failing sink is logged and skipped.</summary>
    Observational = 0,

    /// <summary>Delivery is required: a missing or failing sink is reported to the caller, because the change has already committed and required audit must not be silently lost.</summary>
    Required = 1,
}
