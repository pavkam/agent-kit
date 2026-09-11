// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounds one local schema compilation or instance validation operation.</summary>
/// <remarks>Byte bounds apply to the retained raw UTF-8 JSON value, including internal whitespace. Depth counts the root as one. Work units are deterministic, conservative operations defined by the selected profile; they include comparisons and parsing, not just node visits.</remarks>
public sealed record ToolSchemaLimits
{
    /// <summary>Creates positive limits before any schema or instance is inspected.</summary>
    /// <param name="maximumUtf8Bytes">Maximum raw UTF-8 length of the supplied JSON value.</param>
    /// <param name="maximumDepth">Maximum JSON depth, counting a scalar root as one.</param>
    /// <param name="maximumNodes">Maximum total JSON values, including the root and annotation data.</param>
    /// <param name="maximumWork">Maximum deterministic work units for the complete operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any limit is not positive.</exception>
    public ToolSchemaLimits(int maximumUtf8Bytes, int maximumDepth, int maximumNodes, int maximumWork)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumUtf8Bytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumNodes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWork);
        MaximumUtf8Bytes = maximumUtf8Bytes;
        MaximumDepth = maximumDepth;
        MaximumNodes = maximumNodes;
        MaximumWork = maximumWork;
    }
    /// <summary>Gets the raw JSON byte ceiling checked before decoded-value allocations.</summary>
    /// <value>The positive per-input UTF-8 byte bound.</value>
    public int MaximumUtf8Bytes { get; }
    /// <summary>Gets the permitted root-inclusive JSON depth.</summary>
    /// <value>A positive ceiling additionally constrained by the engine's safe implementation depth.</value>
    public int MaximumDepth { get; }
    /// <summary>Gets the total JSON-value ceiling, including data inside annotations.</summary>
    /// <value>The positive per-input node bound.</value>
    public int MaximumNodes { get; }
    /// <summary>Gets the operation's deterministic work ceiling.</summary>
    /// <value>The positive budget consumed by inspection and validation under the selected profile version.</value>
    public int MaximumWork { get; }
}
