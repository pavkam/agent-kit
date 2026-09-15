// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A content part carrying one model-requested tool call, before it has
/// been authorized, scheduled, or executed.
/// </summary>
/// <remarks>
/// This is the request half of a call; the matching
/// <see cref="ToolResultPart"/> carries the same <see cref="CallId"/> and is
/// the exactly-one terminal result for it. The <see cref="Arguments"/>
/// captured here are exactly what the model produced and have not yet been
/// validated against the tool's input schema — validation, authorization,
/// and execution all happen downstream of this record, never as a side
/// effect of constructing it.
/// </remarks>
public sealed record ToolCallPart: ContentPart
{
    /// <summary>Initializes a new instance of the <see cref="ToolCallPart"/> record.</summary>
    /// <param name="callId">
    /// The stable AgentKit identity correlating this call with its terminal
    /// <see cref="ToolResultPart"/>.
    /// </param>
    /// <param name="tool">The tool being called.</param>
    /// <param name="arguments">The raw, unvalidated call arguments as provided by the model.</param>
    /// <param name="providerCallId">
    /// The provider-supplied call identifier, preserved as external
    /// correlation, when the provider supplies one.
    /// </param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="tool"/> or <paramref name="extensions"/> is null.
    /// </exception>
    public ToolCallPart(
        ToolCallId callId,
        ToolReference tool,
        JsonElement arguments,
        ProviderToolCallId? providerCallId,
        ExtensionData extensions)
        : base(extensions)
    {
        ArgumentNullException.ThrowIfNull(tool);
        CallId = callId;
        Tool = tool;
        Arguments = arguments;
        ProviderCallId = providerCallId;
    }

    /// <summary>
    /// Gets the stable AgentKit identity correlating this call with its
    /// terminal <see cref="ToolResultPart"/>.
    /// </summary>
    public ToolCallId CallId { get; init; }

    /// <summary>Gets the tool being called.</summary>
    public ToolReference Tool { get; init; }

    /// <summary>Gets the raw, unvalidated call arguments as provided by the model.</summary>
    public JsonElement Arguments { get; init; }

    /// <summary>
    /// Gets the provider-supplied call identifier, preserved as external
    /// correlation, when the provider supplies one.
    /// </summary>
    public ProviderToolCallId? ProviderCallId { get; init; }

    /// <summary>
    /// Compares this part structurally: <see cref="Arguments"/> is compared by JSON
    /// value (<see cref="JsonElement.DeepEquals"/>) rather than by backing-document
    /// identity, so a part rebuilt from persisted JSON equals its original.
    /// </summary>
    /// <param name="other">The part to compare with.</param>
    /// <returns><see langword="true"/> when every member, including the JSON arguments, is equal.</returns>
    public bool Equals(ToolCallPart? other) =>
        other is not null
        && CallId.Equals(other.CallId)
        && Tool.Equals(other.Tool)
        && JsonElementValueEquality.Equals(Arguments, other.Arguments)
        && Nullable.Equals(ProviderCallId, other.ProviderCallId)
        && Extensions.Equals(other.Extensions);

    /// <summary>
    /// Hashes the non-JSON members only, so structurally equal arguments with different
    /// textual spellings still hash equally, as <see cref="Equals(ToolCallPart?)"/> requires.
    /// </summary>
    /// <returns>A hash consistent with structural equality.</returns>
    public override int GetHashCode() => HashCode.Combine(CallId, Tool, ProviderCallId, Extensions);
}
