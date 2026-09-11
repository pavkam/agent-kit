// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableExecutionContext behavior and contracts.</summary>
public sealed class DurableExecutionContextTests
{
    [Fact]
    public void DurableExecutionContext_Constructor_WhenAuthorizationIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableExecutionContext(new DurabilityProfileKey("p"), new DurabilityProfileVersion(1), new DurableBackendKey("b"), new DurableJournalKey("j"), new DurableLeaseManagerKey("l"), new RecoveryPolicyKey("r"), null!));
        exception.ParamName.ShouldBe("authorization");
    }

    [Theory]
    [InlineData("profileKey", typeof(ArgumentNullException))]
    [InlineData("backendKey", typeof(ArgumentNullException))]
    [InlineData("journalKey", typeof(ArgumentNullException))]
    [InlineData("leaseManagerKey", typeof(ArgumentNullException))]
    [InlineData("recoveryPolicyKey", typeof(ArgumentNullException))]
    public void DurableExecutionContext_Constructor_WhenRequiredSelectionIsDefault_ThrowsExactParameter(string parameter, Type exceptionType)
    {
        var exception = Record.Exception(() => ContextWithInvalidSelection(parameter));
        _ = exception.ShouldNotBeNull();
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData("ProfileKey", typeof(ArgumentNullException))]
    [InlineData("BackendKey", typeof(ArgumentNullException))]
    [InlineData("JournalKey", typeof(ArgumentNullException))]
    [InlineData("LeaseManagerKey", typeof(ArgumentNullException))]
    [InlineData("RecoveryPolicyKey", typeof(ArgumentNullException))]
    public void DurableExecutionContext_With_WhenRequiredSelectionIsDefault_ThrowsExactPropertyParameter(string property, Type exceptionType)
    {
        var context = DurabilityTestData.Context();
        var exception = Record.Exception(() => ContextWithInvalidCopy(context, property));
        _ = exception.ShouldNotBeNull();
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(property);
    }

    [Fact]
    public void DurableExecutionContext_Constructor_WhenProfileVersionIsZero_RetainsPublishedRevision()
    {
        var context = new DurableExecutionContext(new DurabilityProfileKey("p"), new DurabilityProfileVersion(0), new DurableBackendKey("b"), new DurableJournalKey("j"), new DurableLeaseManagerKey("l"), new RecoveryPolicyKey("r"), DurabilityTestData.Authorization());
        context.ProfileVersion.ShouldBe(new DurabilityProfileVersion(0));
    }

    [Fact]
    public void DurableExecutionContext_With_WhenProfileVersionIsZero_RetainsRevisionWithoutChangingOriginal()
    {
        var original = DurabilityTestData.Context();
        var copy = original with
        {
            ProfileVersion = new DurabilityProfileVersion(0)
        };
        copy.ProfileVersion.ShouldBe(new DurabilityProfileVersion(0));
        original.ProfileVersion.ShouldBe(new DurabilityProfileVersion(1));
    }

    [Fact]
    public void DurableExecutionContext_Constructor_WhenAuthorizationIsCaptured_ExposesOnlyDerivedAuthorizationValues()
    {
        var context = DurabilityTestData.Context();
        context.Authorization.ShouldBe(DurabilityTestData.Authorization());
        context.AuthorizationScope.ShouldBe(context.Authorization.Scope);
        context.AgentDefinitionRevision.ShouldBe(context.Authorization.AgentDefinitionRevision);
        context.ConfigurationVersion.ShouldBe(context.Authorization.ConfigurationVersion);
    }

    private static DurableExecutionContext ContextWithInvalidSelection(string parameter) => new(parameter == "profileKey" ? default : new DurabilityProfileKey("p"), parameter == "profileVersion" ? default : new DurabilityProfileVersion(1), parameter == "backendKey" ? default : new DurableBackendKey("b"), parameter == "journalKey" ? default : new DurableJournalKey("j"), parameter == "leaseManagerKey" ? default : new DurableLeaseManagerKey("l"), parameter == "recoveryPolicyKey" ? default : new RecoveryPolicyKey("r"), DurabilityTestData.Authorization());
    private static DurableExecutionContext ContextWithInvalidCopy(DurableExecutionContext context, string property) => property switch
    {
        "ProfileKey" => context with
        {
            ProfileKey = default
        },
        "ProfileVersion" => context with
        {
            ProfileVersion = default
        },
        "BackendKey" => context with
        {
            BackendKey = default
        },
        "JournalKey" => context with
        {
            JournalKey = default
        },
        "LeaseManagerKey" => context with
        {
            LeaseManagerKey = default
        },
        "RecoveryPolicyKey" => context with
        {
            RecoveryPolicyKey = default
        },
        _ => throw new ArgumentOutOfRangeException(nameof(property)),
    };
}
