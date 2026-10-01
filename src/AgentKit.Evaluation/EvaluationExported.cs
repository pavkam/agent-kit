// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports that an exporter published the report.</summary>
public sealed record EvaluationExported: EvaluationExportResult
{
    /// <summary>Initializes the acknowledgement.</summary>
    /// <param name="location">A safe, non-secret description of where the report went, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="location"/> is present but blank.</exception>
    public EvaluationExported(string? location = null)
    {
        if (location is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(location);
        }

        Location = location;
    }

    /// <summary>Gets a safe description of where the report went, or <see langword="null"/>.</summary>
    public string? Location { get; }
}
