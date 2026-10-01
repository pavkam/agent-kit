// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the retrieval pipeline for authorized candidates relevant to a query.</summary>
/// <remarks>
/// A query carries the complete operation context, so every stage is bound to the authenticated identity and exact memory
/// profile version. It names a classification ceiling and, optionally, the model destination that would receive exposed
/// content. The pipeline can only narrow scope, budget, and classification relative to the profile; it never widens them.
/// </remarks>
public sealed record RetrievalQuery
{
    /// <summary>Initializes a validated query.</summary>
    /// <param name="id">The retrieval request identity.</param>
    /// <param name="context">The operation context.</param>
    /// <param name="query">The query content.</param>
    /// <param name="scope">The narrowing scope.</param>
    /// <param name="budget">The requested budget, narrowed to the profile's ceiling by the pipeline.</param>
    /// <param name="maximumClassification">The highest classification that may be returned.</param>
    /// <param name="exposureDestination">The model destination exposure is authorized for, or <see langword="null"/> when exposure is authorized without a named destination.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The identity is default or the classification is undefined.</exception>
    public RetrievalQuery(
        RetrievalRequestId id,
        MemoryOperationContext context,
        RetrievalQueryContent query,
        RetrievalScope scope,
        RetrievalBudget budget,
        DataClassification maximumClassification,
        ModelDestination? exposureDestination)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentOutOfRangeException.ThrowIfUndefined(maximumClassification);
        Id = id;
        Context = context;
        Query = query;
        Scope = scope;
        Budget = budget;
        MaximumClassification = maximumClassification;
        ExposureDestination = exposureDestination;
    }

    /// <summary>Gets the retrieval request identity.</summary>
    public RetrievalRequestId Id { get; }

    /// <summary>Gets the operation context.</summary>
    public MemoryOperationContext Context { get; }

    /// <summary>Gets the query content.</summary>
    public RetrievalQueryContent Query { get; }

    /// <summary>Gets the narrowing scope.</summary>
    public RetrievalScope Scope { get; }

    /// <summary>Gets the requested budget.</summary>
    public RetrievalBudget Budget { get; }

    /// <summary>Gets the highest classification that may be returned.</summary>
    public DataClassification MaximumClassification { get; }

    /// <summary>Gets the model destination exposure is authorized for, or <see langword="null"/>.</summary>
    public ModelDestination? ExposureDestination { get; }

    /// <summary>Creates the query with a different content, keeping every other value.</summary>
    /// <param name="query">The replacement content.</param>
    /// <returns>A copy carrying <paramref name="query"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    public RetrievalQuery WithQuery(RetrievalQueryContent query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new(Id, Context, query, Scope, Budget, MaximumClassification, ExposureDestination);
    }

    /// <summary>Creates the query with a different budget, keeping every other value.</summary>
    /// <param name="budget">The replacement budget.</param>
    /// <returns>A copy carrying <paramref name="budget"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="budget"/> is null.</exception>
    public RetrievalQuery WithBudget(RetrievalBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        return new(Id, Context, Query, Scope, budget, MaximumClassification, ExposureDestination);
    }
}
