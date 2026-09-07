// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The stable, first-party <c>agentkit.*</c> budget dimension keys.
/// </summary>
/// <remarks>
/// AgentKit reserves the <c>agentkit.*</c> namespace for these dimensions.
/// Third-party dimensions must use their own stable namespace and register
/// a <see cref="BudgetDimensionDescriptor"/> declaring their aggregation
/// semantics and legal units.
/// </remarks>
public static class BudgetDimensions
{
    /// <summary>Gets the dimension counting turns taken within a run.</summary>
    public static BudgetDimension Turns { get; } = new("agentkit.turns");

    /// <summary>Gets the dimension counting steps taken within a run.</summary>
    public static BudgetDimension Steps { get; } = new("agentkit.steps");

    /// <summary>Gets the dimension counting model requests.</summary>
    public static BudgetDimension ModelRequests { get; } = new("agentkit.model.requests");

    /// <summary>Gets the dimension counting concurrently in-flight model requests.</summary>
    public static BudgetDimension ConcurrentModelRequests { get; } = new("agentkit.model.concurrent_requests");

    /// <summary>Gets the dimension counting consumed input tokens.</summary>
    public static BudgetDimension InputTokens { get; } = new("agentkit.tokens.input");

    /// <summary>Gets the dimension counting produced output tokens.</summary>
    public static BudgetDimension OutputTokens { get; } = new("agentkit.tokens.output");

    /// <summary>Gets the dimension counting hidden reasoning tokens.</summary>
    public static BudgetDimension ReasoningTokens { get; } = new("agentkit.tokens.reasoning");

    /// <summary>Gets the dimension counting cached-read input tokens.</summary>
    public static BudgetDimension CachedReadTokens { get; } = new("agentkit.tokens.cached_read");

    /// <summary>Gets the dimension counting cached-write input tokens.</summary>
    public static BudgetDimension CachedWriteTokens { get; } = new("agentkit.tokens.cached_write");

    /// <summary>Gets the dimension bounding input tokens for a single request.</summary>
    public static BudgetDimension PerRequestInputTokens { get; } = new("agentkit.request.input_tokens");

    /// <summary>Gets the dimension bounding requested output tokens for a single request.</summary>
    public static BudgetDimension PerRequestOutputTokens { get; } = new("agentkit.request.output_tokens");

    /// <summary>Gets the dimension accumulating estimated or reported cost.</summary>
    public static BudgetDimension Cost { get; } = new("agentkit.cost");

    /// <summary>Gets the dimension counting attempted tool calls.</summary>
    public static BudgetDimension AttemptedToolCalls { get; } = new("agentkit.tools.attempted");

    /// <summary>Gets the dimension counting successful tool calls.</summary>
    public static BudgetDimension SuccessfulToolCalls { get; } = new("agentkit.tools.successful");

    /// <summary>Gets the dimension counting concurrently executing tool calls.</summary>
    public static BudgetDimension ConcurrentToolCalls { get; } = new("agentkit.tools.concurrent");

    /// <summary>Gets the dimension counting tool retry attempts.</summary>
    public static BudgetDimension ToolRetries { get; } = new("agentkit.tools.retries");

    /// <summary>Gets the dimension counting output-validation retry attempts.</summary>
    public static BudgetDimension OutputValidationRetries { get; } = new("agentkit.output.validation_retries");

    /// <summary>Gets the dimension counting delegations.</summary>
    public static BudgetDimension Delegations { get; } = new("agentkit.delegations");

    /// <summary>Gets the dimension counting concurrently active delegations.</summary>
    public static BudgetDimension ConcurrentDelegations { get; } = new("agentkit.delegations.concurrent");

    /// <summary>Gets the dimension accumulating wall-clock run duration.</summary>
    public static BudgetDimension RunElapsedTime { get; } = new("agentkit.time.run");

    /// <summary>Gets the dimension accumulating wall-clock operation duration.</summary>
    public static BudgetDimension OperationElapsedTime { get; } = new("agentkit.time.operation");

    /// <summary>Gets the dimension bounding assembled context size in bytes.</summary>
    public static BudgetDimension ContextBytes { get; } = new("agentkit.context.bytes");

    /// <summary>Gets the dimension bounding assembled context size in tokens.</summary>
    public static BudgetDimension ContextTokens { get; } = new("agentkit.context.tokens");

    /// <summary>Gets the dimension bounding retained media size in bytes.</summary>
    public static BudgetDimension RetainedMediaBytes { get; } = new("agentkit.context.media_bytes");

    /// <summary>Gets the dimension counting queued admitted inputs.</summary>
    public static BudgetDimension QueuedInputCount { get; } = new("agentkit.queue.inputs");

    /// <summary>Gets the dimension bounding queued input size in bytes.</summary>
    public static BudgetDimension QueuedInputBytes { get; } = new("agentkit.queue.bytes");

    /// <summary>Gets the dimension bounding queued input age.</summary>
    public static BudgetDimension QueuedInputAge { get; } = new("agentkit.queue.age");

    /// <summary>Gets the dimension bounding streamed event size in bytes.</summary>
    public static BudgetDimension EventBytes { get; } = new("agentkit.events.bytes");

    /// <summary>Gets the dimension bounding final-result size in bytes.</summary>
    public static BudgetDimension ResultBytes { get; } = new("agentkit.results.bytes");

    /// <summary>Gets the dimension bounding tool result size in bytes.</summary>
    public static BudgetDimension ToolResultBytes { get; } = new("agentkit.tools.result_bytes");

    /// <summary>Gets the dimension counting retrieved items.</summary>
    public static BudgetDimension RetrievedItemCount { get; } = new("agentkit.retrieval.items");

    /// <summary>Gets the dimension bounding retrieved content size in bytes.</summary>
    public static BudgetDimension RetrievedBytes { get; } = new("agentkit.retrieval.bytes");

    /// <summary>Gets the dimension bounding buffered stream size in bytes.</summary>
    public static BudgetDimension BufferedStreamBytes { get; } = new("agentkit.stream.buffered_bytes");

    /// <summary>Gets the dimension bounding durable artifact size in bytes.</summary>
    public static BudgetDimension ArtifactBytes { get; } = new("agentkit.artifacts.bytes");
}
