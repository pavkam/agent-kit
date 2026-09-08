// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports the immutable publication matching one exact reader query.</summary>
public sealed record SecurityProfilePublicationFound: SecurityProfilePublicationResult
{
    /// <summary>Initializes a successful exact-publication result.</summary><param name="publication">The non-null immutable publication.</param><exception cref="ArgumentNullException"><paramref name="publication"/> is null.</exception>
    public SecurityProfilePublicationFound(SecurityProfilePublication publication) { ArgumentNullException.ThrowIfNull(publication); Publication = publication; }
    /// <summary>Gets the exact immutable publication.</summary><value>The non-null published composition evidence.</value>
    public SecurityProfilePublication Publication { get; }
}
