// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One bounded, trust-tagged unit of content a contributor proposes for assembly.</summary>
/// <remarks>
/// Candidates carry provenance and cost evidence only; inclusion, ordering, and instruction elevation are decided
/// later by the assembler and budget allocator.
/// </remarks>
public sealed record ContextCandidate
{
    /// <summary>Initializes one contributor candidate.</summary>
    /// <param name="source">The exact publication this candidate came from.</param>
    /// <param name="kind">How the candidate may be projected.</param>
    /// <param name="trust">The provenance trust retained with the content.</param>
    /// <param name="priority">The relative priority used when budgets require trimming among optional candidates.</param>
    /// <param name="scope">The lifecycle scope at which the candidate was captured.</param>
    /// <param name="cost">The estimated byte and token cost of the candidate content.</param>
    /// <param name="freshness">Freshness evidence for the captured content.</param>
    /// <param name="frequency">How often the contributor may re-evaluate for this run.</param>
    /// <param name="mandatory">Whether omission must fail assembly when the candidate cannot fit.</param>
    /// <param name="content">The immutable content parts proposed for projection.</param>
    /// <param name="extensions">Forward-compatible contributor metadata.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="freshness"/>, or <paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum field is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="content"/> is a default, uninitialized array.</exception>
    public ContextCandidate(
        ContextSourceReference source,
        ContextCandidateKind kind,
        ContextTrust trust,
        int priority,
        ContextScope scope,
        ContextCostEstimate cost,
        ContextFreshness freshness,
        ContextEvaluationFrequency frequency,
        bool mandatory,
        ImmutableArray<ContentPart> content,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(trust);
        ArgumentOutOfRangeException.ThrowIfUndefined(scope);
        ArgumentOutOfRangeException.ThrowIfUndefined(frequency);
        ArgumentNullException.ThrowIfNull(freshness);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentException.ThrowIfDefault(content);

        Source = source;
        Kind = kind;
        Trust = trust;
        Priority = priority;
        Scope = scope;
        Cost = cost;
        Freshness = freshness;
        Frequency = frequency;
        Mandatory = mandatory;
        Content = content;
        Extensions = extensions;
    }

    /// <summary>Gets the exact publication this candidate came from.</summary>
    public ContextSourceReference Source { get; }

    /// <summary>Gets how the candidate may be projected.</summary>
    public ContextCandidateKind Kind { get; }

    /// <summary>Gets the provenance trust retained with the content.</summary>
    public ContextTrust Trust { get; }

    /// <summary>Gets the relative priority used when budgets require trimming among optional candidates.</summary>
    public int Priority { get; }

    /// <summary>Gets the lifecycle scope at which the candidate was captured.</summary>
    public ContextScope Scope { get; }

    /// <summary>Gets the estimated byte and token cost of the candidate content.</summary>
    public ContextCostEstimate Cost { get; }

    /// <summary>Gets freshness evidence for the captured content.</summary>
    public ContextFreshness Freshness { get; }

    /// <summary>Gets how often the contributor may re-evaluate for this run.</summary>
    public ContextEvaluationFrequency Frequency { get; }

    /// <summary>Gets whether omission must fail assembly when the candidate cannot fit.</summary>
    public bool Mandatory { get; }

    /// <summary>Gets the immutable content parts proposed for projection.</summary>
    public ImmutableArray<ContentPart> Content { get; }

    /// <summary>Gets forward-compatible contributor metadata.</summary>
    public ExtensionData Extensions { get; }
}
