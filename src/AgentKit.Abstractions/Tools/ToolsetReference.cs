// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;

/// <summary>References a toolset and execution-policy family without resolving either.</summary>
/// <remarks>Publication captures concrete versions and source membership later; this local value performs no lookup or authority grant.</remarks>
public sealed record ToolsetReference
{
    /// <summary>Creates a declarative selection without inventing publication or policy versions.</summary>
    /// <param name="key">The nondefault authored toolset key.</param>
    /// <param name="executionPolicyKey">The nondefault execution-policy family selected for capture.</param>
    /// <exception cref="ArgumentOutOfRangeException">A key is default.</exception>
    public ToolsetReference(ToolsetKey key, ToolExecutionPolicyKey executionPolicyKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionPolicyKey, default);
        Key = key;
        ExecutionPolicyKey = executionPolicyKey;
    }
    /// <summary>Gets the selected toolset family.</summary>
    /// <value>A nondefault exact key resolved against an immutable publication later.</value>
    public ToolsetKey Key { get; }

    /// <summary>Gets the selected execution-policy family.</summary>
    /// <value>A nondefault key whose exact version is retained during compilation.</value>
    public ToolExecutionPolicyKey ExecutionPolicyKey { get; }
}
