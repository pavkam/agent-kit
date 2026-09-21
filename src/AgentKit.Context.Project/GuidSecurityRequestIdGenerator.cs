// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project;

/// <summary>Creates random <see cref="SecurityRequestId"/> values for project instruction reads.</summary>
internal sealed class GuidSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    /// <inheritdoc/>
    public SecurityRequestId Create() => new(Guid.NewGuid());
}
