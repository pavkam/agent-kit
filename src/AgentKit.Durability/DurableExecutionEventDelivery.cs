// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Declares how strictly one durable execution event sink's delivery is enforced.</summary>
public enum DurableExecutionEventDelivery
{
    /// <summary>Delivery is observational, so an unavailable or failing sink never changes the durable outcome.</summary>
    Observational,

    /// <summary>Delivery is required, so an unavailable or failing sink fails the publishing operation closed.</summary>
    Required,
}
