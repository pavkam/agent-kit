// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Distinguishes the kind of subject an <see cref="ExecutionIdentity"/>
/// represents.
/// </summary>
public enum ExecutionSubjectKind
{
    /// <summary>A human user authenticated through an interactive flow.</summary>
    Human,

    /// <summary>A service account acting without a human present.</summary>
    Service,

    /// <summary>An automated workload, such as a scheduled job or another agent.</summary>
    Workload,

    /// <summary>An unauthenticated subject, when the deployment explicitly allows it.</summary>
    Anonymous
}
