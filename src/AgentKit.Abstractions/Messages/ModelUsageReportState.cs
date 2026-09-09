// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes the lifecycle state of usage evidence reported by a model provider.</summary>
/// <remarks>The state belongs to usage evidence, independently of response or transport completion.</remarks>
public enum ModelUsageReportState
{
    /// <summary>The provider supplied no usage evidence; this state grants no authority.</summary>
    NotReported,

    /// <summary>The provider identified the report as partial or cumulative so far.</summary>
    Interim,

    /// <summary>The provider identified the report as its final usage evidence.</summary>
    Final,
}
