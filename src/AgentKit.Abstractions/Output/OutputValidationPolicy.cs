// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares how validator failures accumulate for one <see cref="OutputDefinition"/>.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record OutputValidationPolicy
{
    /// <summary>Gets the shared instance that stops at the first failing validator, reporting at most one issue.</summary>
    public static OutputValidationPolicy RejectOnFirstFailure { get; } =
        new(OutputValidationFailureMode.RejectOnFirstFailure, 1);

    /// <summary>Initializes a new instance of the <see cref="OutputValidationPolicy"/> record.</summary>
    /// <param name="failureMode">Whether validation stops at the first failure or collects every failure.</param>
    /// <param name="maximumIssues">The maximum number of issues retained in a validation failure.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="failureMode"/> is undefined, or <paramref name="maximumIssues"/> is not positive.
    /// </exception>
    public OutputValidationPolicy(OutputValidationFailureMode failureMode, int maximumIssues)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(failureMode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIssues);

        FailureMode = failureMode;
        MaximumIssues = maximumIssues;
    }

    /// <summary>Gets whether validation stops at the first failure or collects every failure.</summary>
    public OutputValidationFailureMode FailureMode { get; init; }

    /// <summary>Gets the maximum number of issues retained in a validation failure.</summary>
    public int MaximumIssues { get; init; }
}
