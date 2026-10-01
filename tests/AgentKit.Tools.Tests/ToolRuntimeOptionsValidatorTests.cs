// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

/// <summary>Verifies <see cref="ToolRuntimeOptionsValidator"/> rejects impossible runtime limits at the composition boundary.</summary>
public sealed class ToolRuntimeOptionsValidatorTests
{
    [Fact]
    public void Validate_WhenOptionsAreTheDefaults_Succeeds() =>
        new ToolRuntimeOptionsValidator().Validate(null, new ToolRuntimeOptions()).Succeeded.ShouldBeTrue();

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Validate_WhenALimitIsImpossible_FailsAndNamesTheOption(string expected, Action<ToolRuntimeOptions> mutate)
    {
        var options = new ToolRuntimeOptions();
        mutate(options);

        var result = new ToolRuntimeOptionsValidator().Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenSeveralLimitsAreImpossible_ReportsEveryViolation()
    {
        var options = new ToolRuntimeOptions { MaximumAttempts = 0, EventSinkTimeout = TimeSpan.Zero };

        var result = new ToolRuntimeOptionsValidator().Validate(null, options);

        result.Failures!.Count().ShouldBe(2);
    }

    [Fact]
    public void Validate_WhenOptionsAreNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolRuntimeOptionsValidator().Validate(null, null!)).ParamName.ShouldBe("options");

    public static TheoryData<string, Action<ToolRuntimeOptions>> InvalidOptions => new()
    {
        { "MaximumInvocationTimeout", static options => options.MaximumInvocationTimeout = options.InvocationTimeout - TimeSpan.FromSeconds(1) },
        { "InvocationDrainPeriod", static options => options.InvocationDrainPeriod = TimeSpan.FromSeconds(-1) },
        { "ResultSpillPreviewBytes", static options => options.ResultSpillPreviewBytes = -1 },
        { "MaximumArgumentBytes", static options => options.MaximumArgumentBytes = 0 },
        { "MaximumResultBytes", static options => options.MaximumResultBytes = 0 },
        { "MaximumParallelInvocations", static options => options.MaximumParallelInvocations = 0 },
        { "MaximumAttempts", static options => options.MaximumAttempts = 0 },
        { "InvocationTimeout", static options => options.InvocationTimeout = TimeSpan.Zero },
        { "RetryInitialDelay", static options => options.RetryInitialDelay = TimeSpan.FromSeconds(-1) },
        { "RetryBackoffMultiplier", static options => options.RetryBackoffMultiplier = 0.5 },
        { "RetryBackoffMultiplier", static options => options.RetryBackoffMultiplier = double.NaN },
        { "RetryMaximumDelay", static options => options.RetryMaximumDelay = TimeSpan.Zero },
        { "RetryJitterFraction", static options => options.RetryJitterFraction = 1.5 },
        { "MaximumRecordAppendAttempts", static options => options.MaximumRecordAppendAttempts = 0 },
        { "AcceptedRecordLookupEntries", static options => options.AcceptedRecordLookupEntries = 0 },
        { "EventSinkTimeout", static options => options.EventSinkTimeout = TimeSpan.Zero },
        { "BatchFailureMode", static options => options.BatchFailureMode = (ToolBatchFailureMode) 99 },
        { "UnknownSchedulingMode", static options => options.UnknownSchedulingMode = (UnknownSchedulingMode) 99 },
        { "ArgumentValidationLimits", static options => options.ArgumentValidationLimits = null! },
    };
}
