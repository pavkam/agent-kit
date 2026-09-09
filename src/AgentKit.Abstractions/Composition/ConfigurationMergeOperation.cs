// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names one declared configuration merge operation.</summary>
public enum ConfigurationMergeOperation
{
    /// <summary>Uses the highest-precedence present value.</summary>
    Replace,
    /// <summary>Recursively merges object members.</summary>
    DeepMerge,
    /// <summary>Concatenates ordered values.</summary>
    Append,
    /// <summary>Merges values selected by stable key.</summary>
    KeyedMerge,
    /// <summary>Concatenates ordered evaluation rules.</summary>
    RuleList,
    /// <summary>Removes an inherited value when permitted.</summary>
    Reset,
}
