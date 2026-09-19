// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using System.Text.Json;

/// <summary>Verifies <see cref="BeforeToolInvocationEventArgs"/> argument checks, mutation limits, veto, and isolation state.</summary>
public sealed class BeforeToolInvocationEventArgsTests
{
    private static BeforeToolInvocationEventArgs Create(ToolCallPart? call = null) => new(
        HookPointEventArgsTestData.TurnDispatch(AgentHookPoints.BeforeToolInvocation), HookPointEventArgsTestData.AgentId,
        HookPointEventArgsTestData.SessionId, call ?? HookPointEventArgsTestData.Call());

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesTheCallAndStartsUnvetoed()
    {
        var call = HookPointEventArgsTestData.Call();

        var args = Create(call);

        args.Call.ShouldBeSameAs(call);
        args.CallId.ShouldBe(call.CallId);
        args.Tool.ShouldBe(call.Tool);
        args.ArgumentsChanged.ShouldBeFalse();
        args.Veto.ShouldBeNull();
        args.IsShortCircuited.ShouldBeFalse();
        args.RunId.ShouldBe(HookPointEventArgsTestData.TurnCorrelation.RunId);
        args.TurnId.ShouldBe(HookPointEventArgsTestData.TurnCorrelation.TurnId!.Value);
    }

    [Fact]
    public void Constructor_WhenDispatchIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new BeforeToolInvocationEventArgs(
            null!, HookPointEventArgsTestData.AgentId, HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.Call()))
            .ParamName.ShouldBe("dispatch");

    [Fact]
    public void Constructor_WhenCallIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new BeforeToolInvocationEventArgs(
            HookPointEventArgsTestData.TurnDispatch(AgentHookPoints.BeforeToolInvocation), HookPointEventArgsTestData.AgentId,
            HookPointEventArgsTestData.SessionId, null!))
            .ParamName.ShouldBe("call");

    [Fact]
    public void Constructor_WhenCorrelationNamesNoTurn_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new BeforeToolInvocationEventArgs(
            HookPointEventArgsTestData.RunDispatch(AgentHookPoints.BeforeToolInvocation), HookPointEventArgsTestData.AgentId,
            HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.Call()))
            .ParamName.ShouldBe("dispatch");

    [Fact]
    public void Veto_WhenSet_ShortCircuits()
    {
        var args = Create();

        args.Veto = new ToolInvocationVeto("not today");

        args.IsShortCircuited.ShouldBeTrue();
        Should.NotThrow(args.Validate);
    }

    [Fact]
    public void Validate_WhenArgumentsStayAnObject_Passes()
    {
        var args = Create();

        args.Arguments = JsonDocument.Parse("""{"path":"b.txt"}""").RootElement.Clone();

        Should.NotThrow(args.Validate);
        args.ArgumentsChanged.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenArgumentsChangeKind_ThrowsHookValidationException()
    {
        var args = Create();

        args.Arguments = JsonDocument.Parse("[1]").RootElement.Clone();

        _ = Should.Throw<HookValidationException>(args.Validate);
    }

    [Fact]
    public void Validate_WhenArgumentsBecomeUndefined_ThrowsHookValidationException()
    {
        var args = Create();

        args.Arguments = default;

        _ = Should.Throw<HookValidationException>(args.Validate);
    }

    [Fact]
    public void Validate_WhenTheOriginalHadNoArguments_AllowsUndefinedOrAnObjectOnly()
    {
        var call = new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("t"), null, null), default, null, ExtensionData.Empty);
        var args = Create(call);

        Should.NotThrow(args.Validate);
        args.Arguments = JsonDocument.Parse("{}").RootElement.Clone();
        Should.NotThrow(args.Validate);
        args.Arguments = JsonDocument.Parse("1").RootElement.Clone();
        _ = Should.Throw<HookValidationException>(args.Validate);
    }

    [Fact]
    public void RestoreMutableState_WhenGivenACapturedSnapshot_RestoresArgumentsAndVeto()
    {
        var args = Create();
        var snapshot = args.CaptureMutableState();
        args.Arguments = JsonDocument.Parse("""{"path":"c.txt"}""").RootElement.Clone();
        args.Veto = new ToolInvocationVeto("no");

        args.RestoreMutableState(snapshot);

        args.ArgumentsChanged.ShouldBeFalse();
        args.Veto.ShouldBeNull();
    }

    [Fact]
    public void RestoreMutableState_WhenGivenAForeignSnapshot_LeavesStateUntouched()
    {
        var args = Create();
        args.Veto = new ToolInvocationVeto("no");

        args.RestoreMutableState(42);

        _ = args.Veto.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void ToolInvocationVeto_WhenReasonIsBlank_ThrowsArgumentException(string? reason) =>
        Should.Throw<ArgumentException>(() => new ToolInvocationVeto(reason!)).ParamName.ShouldBe("safeReason");
}
