// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Stable <see cref="CompositionDiagnostic"/> codes produced by <see cref="HookOrderResolver"/>.</summary>
/// <remarks>
/// Hosts and composition validation match these identifiers programmatically. The human-readable diagnostic
/// message may change; these codes do not. Every code uses the <c>agentkit.hook-order.*</c> prefix.
/// </remarks>
public static class HookOrderDiagnosticCodes
{
    /// <summary>The same <see cref="HookRegistrationId"/> appears more than once in one resolution scope.</summary>
    public const string DuplicateRegistration = "agentkit.hook-order.duplicate-registration";

    /// <summary>More than one registration in the resolution scope claims <see cref="HookOrderAnchor.First"/>.</summary>
    public const string MultipleFirst = "agentkit.hook-order.multiple-first";

    /// <summary>More than one registration in the resolution scope claims <see cref="HookOrderAnchor.Last"/>.</summary>
    public const string MultipleLast = "agentkit.hook-order.multiple-last";

    /// <summary>A registration names its own identity in <c>Before</c>, <c>After</c>, or <c>DependsOn</c>.</summary>
    public const string SelfReference = "agentkit.hook-order.self-reference";

    /// <summary>A hard <c>DependsOn</c> target is absent from the same point, profile, and catalog resolution.</summary>
    public const string MissingDependency = "agentkit.hook-order.missing-dependency";

    /// <summary>One registration names the same target in ordering relations that require opposite directions.</summary>
    public const string ContradictoryEdges = "agentkit.hook-order.contradictory-edges";

    /// <summary>An explicit edge would place a registration on the wrong side of a <see cref="HookOrderAnchor.First"/> or <see cref="HookOrderAnchor.Last"/> anchor.</summary>
    public const string AnchorContradiction = "agentkit.hook-order.anchor-contradiction";

    /// <summary>The remaining ordering edges, including anchor edges, contain a direct or transitive cycle.</summary>
    public const string Cycle = "agentkit.hook-order.cycle";
}
