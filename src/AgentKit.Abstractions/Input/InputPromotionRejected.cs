// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports authorization, address, or planning rejection before any input-promotion mutation occurs.</summary>
/// <remarks>The rejection contains no promotion receipt because it neither consumes pending input nor advances the target turn.</remarks>
public sealed record InputPromotionRejected: InputPromotionResult
{
    /// <summary>Initializes a pre-mutation promotion rejection.</summary>
    /// <param name="rejection">The non-null typed content-free failure that prevented promotion.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is null.</exception>
    public InputPromotionRejected(InputRejection rejection) { ArgumentNullException.ThrowIfNull(rejection); Rejection = rejection; }
    /// <summary>Gets the typed failure that prevented promotion.</summary>
    /// <value>A non-null content-free rejection; callers must not infer a changed queue or target-turn state from it.</value>
    public InputRejection Rejection { get; }
}
