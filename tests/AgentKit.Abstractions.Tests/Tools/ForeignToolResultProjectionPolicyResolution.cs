// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

internal sealed record ForeignToolResultProjectionPolicyResolution: ToolResultProjectionPolicyResolution
{
    public ForeignToolResultProjectionPolicyResolution()
    {
    }

    public ForeignToolResultProjectionPolicyResolution(ToolResultProjectionPolicyResolution original) : base(original)
    {
    }

    public override ToolResultProjectionPolicyReference Reference => new(new("foreign"), new(1));
}
