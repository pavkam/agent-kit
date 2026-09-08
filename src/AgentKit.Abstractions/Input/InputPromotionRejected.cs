// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports authorization, address, or planning rejection before promotion mutation.</summary>
public sealed record InputPromotionRejected: InputPromotionResult
{
    /// <summary>Initializes promotion rejection.</summary>
    public InputPromotionRejected(InputRejection rejection) { ArgumentNullException.ThrowIfNull(rejection); Rejection = rejection; }
    /// <summary>Gets rejection.</summary><value>The typed content-free failure.</value>
    public InputRejection Rejection { get; }
}
