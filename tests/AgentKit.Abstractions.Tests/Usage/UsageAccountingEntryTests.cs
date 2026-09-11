// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Usage;
/// <summary>Verifies UsageAccountingEntry behavior and contracts.</summary>
public sealed class UsageAccountingEntryTests
{
    [Theory]
    [InlineData("id")]
    [InlineData("runId")]
    [InlineData("operationId")]
    [InlineData("revision")]
    [InlineData("previousRevision")]
    public void Constructor_WhenEntryIdentityOrRevisionIsDefault_RejectsExactArgument(string parameter)
    {
        var basis = RunUsageTests.Entry(1, []);
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new UsageAccountingEntry(parameter == "id" ? default : basis.Id, parameter == "runId" ? default : basis.RunId, parameter == "operationId" ? default : basis.OperationId, parameter == "revision" ? default : basis.Revision, parameter == "previousRevision" ? default(UsageAccountingRevision) : null, [], null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    public void Constructor_WhenEntryRevisionLinkageIsInvalid_RejectsExactArgument(long revision, long previous)
    {
        var basis = RunUsageTests.Entry(1, []);
        Should.Throw<ArgumentException>(() => new UsageAccountingEntry(basis.Id, basis.RunId, basis.OperationId, new(revision), previous == 0 ? null : new UsageAccountingRevision(previous), [], null, null, ExtensionData.Empty)).ParamName.ShouldBe("previousRevision");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenModelAndProviderReportPresenceDiffer_RejectsExactArgument(bool hasModel)
    {
        var basis = RunUsageTests.Entry(1, []);
        Should.Throw<ArgumentException>(() => new UsageAccountingEntry(basis.Id, basis.RunId, basis.OperationId, new(1), null, [], hasModel ? RunUsageTests.Model : null, hasModel ? null : ModelUsage.NotReported, ExtensionData.Empty)).ParamName.ShouldBe("providerUsage");
    }

    [Fact]
    public void Constructor_WhenEntryExtensionsAreNull_RejectsExactArgument()
    {
        var basis = RunUsageTests.Entry(1, []);
        Should.Throw<ArgumentNullException>(() => new UsageAccountingEntry(basis.Id, basis.RunId, basis.OperationId, new(1), null, [], null, null, null!)).ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Apply_WhenEntryIsNull_RejectsBeforeCreatingProjection() => Should.Throw<ArgumentNullException>(() => new RunUsage(RunUsageTests.Run, []).Apply(null!)).ParamName.ShouldBe("entry");
}
