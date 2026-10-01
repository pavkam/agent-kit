// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Scores a stored vector against a query under one distance metric, always higher-is-better.</summary>
internal static class VectorScoring
{
    /// <summary>Scores one vector.</summary>
    /// <param name="metric">The space's metric.</param>
    /// <param name="query">The query components.</param>
    /// <param name="stored">The stored components; both vectors have the space's dimensions.</param>
    /// <returns>Cosine similarity, the negated Euclidean distance, or the dot product.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="metric"/> is undefined.</exception>
    internal static double Score(VectorDistanceMetric metric, ReadOnlySpan<float> query, ReadOnlySpan<float> stored)
    {
        Debug.Assert(query.Length == stored.Length, "The space fixes the dimensions of every vector.");
        double dot = 0;
        double queryNorm = 0;
        double storedNorm = 0;
        double squaredDistance = 0;
        for (var index = 0; index < query.Length; index++)
        {
            double left = query[index];
            double right = stored[index];
            dot += left * right;
            queryNorm += left * left;
            storedNorm += right * right;
            var difference = left - right;
            squaredDistance += difference * difference;
        }

        return metric switch
        {
            VectorDistanceMetric.Cosine => queryNorm == 0 || storedNorm == 0 ? 0 : dot / (Math.Sqrt(queryNorm) * Math.Sqrt(storedNorm)),
            VectorDistanceMetric.Euclidean => -Math.Sqrt(squaredDistance),
            VectorDistanceMetric.DotProduct => dot,
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "The distance metric is undefined."),
        };
    }
}
