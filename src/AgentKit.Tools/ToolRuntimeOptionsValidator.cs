// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.Options;

/// <summary>Validates <see cref="ToolRuntimeOptions"/> at the composition boundary so impossible limits fail before a run starts.</summary>
/// <remarks>The validator is stateless and thread-safe. It reports every violation at once and never mutates the options.</remarks>
internal sealed class ToolRuntimeOptionsValidator: IValidateOptions<ToolRuntimeOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ToolRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        Require(options.MaximumArgumentBytes > 0, "MaximumArgumentBytes must be positive.", failures);
        Require(options.MaximumResultBytes > 0, "MaximumResultBytes must be positive.", failures);
        Require(options.MaximumParallelInvocations > 0, "MaximumParallelInvocations must be positive.", failures);
        Require(options.MaximumAttempts > 0, "MaximumAttempts must be positive and counts the first attempt.", failures);
        Require(options.InvocationTimeout > TimeSpan.Zero, "InvocationTimeout must be positive.", failures);
        Require(options.RetryInitialDelay >= TimeSpan.Zero, "RetryInitialDelay must not be negative.", failures);
        Require(double.IsFinite(options.RetryBackoffMultiplier) && options.RetryBackoffMultiplier >= 1.0, "RetryBackoffMultiplier must be a finite value of at least one.", failures);
        Require(options.RetryMaximumDelay >= options.RetryInitialDelay, "RetryMaximumDelay must not be less than RetryInitialDelay.", failures);
        Require(double.IsFinite(options.RetryJitterFraction) && options.RetryJitterFraction is >= 0.0 and <= 1.0, "RetryJitterFraction must be between zero and one.", failures);
        Require(options.MaximumRecordAppendAttempts > 0, "MaximumRecordAppendAttempts must be positive.", failures);
        Require(options.AcceptedRecordLookupEntries > 0, "AcceptedRecordLookupEntries must be positive.", failures);
        Require(options.EventSinkTimeout > TimeSpan.Zero, "EventSinkTimeout must be positive.", failures);
        Require(Enum.IsDefined(options.BatchFailureMode), "BatchFailureMode must be a defined value.", failures);
        Require(Enum.IsDefined(options.UnknownSchedulingMode), "UnknownSchedulingMode must be a defined value.", failures);
        Require(options.ArgumentValidationLimits is not null, "ArgumentValidationLimits must be set.", failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Require(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}
