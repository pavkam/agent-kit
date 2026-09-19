// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookPointDefinitionTests
{
    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookPointDefinition<HookKernelTestData.ITestHook, HookKernelTestData.TestEventArgs>(
            default, HookPointKind.Observational, HookFailureMode.Isolate, new HookKernelTestData.TestValidator(), HookKernelTestData.Invoke));

        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookPointDefinition<HookKernelTestData.ITestHook, HookKernelTestData.TestEventArgs>(
            HookKernelTestData.Point, (HookPointKind) 99, HookFailureMode.FailOperation, new HookKernelTestData.TestValidator(), HookKernelTestData.Invoke));

        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Constructor_WhenFailureInvariantIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookPointDefinition<HookKernelTestData.ITestHook, HookKernelTestData.TestEventArgs>(
            HookKernelTestData.Point, HookPointKind.Observational, (HookFailureMode) 99, new HookKernelTestData.TestValidator(), HookKernelTestData.Invoke));

        exception.ParamName.ShouldBe("failureInvariant");
    }

    [Fact]
    public void Constructor_WhenValidatorIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookPointDefinition<HookKernelTestData.ITestHook, HookKernelTestData.TestEventArgs>(
            HookKernelTestData.Point, HookPointKind.Observational, HookFailureMode.Isolate, null!, HookKernelTestData.Invoke));

        exception.ParamName.ShouldBe("validator");
    }

    [Fact]
    public void Constructor_WhenInvokeIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookPointDefinition<HookKernelTestData.ITestHook, HookKernelTestData.TestEventArgs>(
            HookKernelTestData.Point, HookPointKind.Observational, HookFailureMode.Isolate, new HookKernelTestData.TestValidator(), null!));

        exception.ParamName.ShouldBe("invoke");
    }

    [Theory]
    [InlineData(HookPointKind.Mutating)]
    [InlineData(HookPointKind.ShortCircuiting)]
    public void Constructor_WhenNonObservationalKindDeclaresIsolate_ThrowsArgumentOutOfRangeException(HookPointKind kind)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookPointDefinition<HookKernelTestData.ITestHook, HookKernelTestData.TestEventArgs>(
            HookKernelTestData.Point, kind, HookFailureMode.Isolate, new HookKernelTestData.TestValidator(), HookKernelTestData.Invoke));

        exception.ParamName.ShouldBe("failureInvariant");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsEveryProperty()
    {
        var validator = new HookKernelTestData.TestValidator();

        var definition = new HookPointDefinition<HookKernelTestData.ITestHook, HookKernelTestData.TestEventArgs>(
            HookKernelTestData.Point, HookPointKind.Observational, HookFailureMode.Isolate, validator, HookKernelTestData.Invoke);

        definition.Id.ShouldBe(HookKernelTestData.Point);
        definition.Kind.ShouldBe(HookPointKind.Observational);
        definition.FailureInvariant.ShouldBe(HookFailureMode.Isolate);
        definition.Validator.ShouldBe(validator);
        definition.Invoke.ShouldBe(HookKernelTestData.Invoke);
    }
}
