// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

using System.Reflection;

public sealed class McpToolContractValidationTests
{
    public static TheoryData<Type, string> InvalidContracts => new()
    {
        { typeof(NoAttributedTools), "does not declare any attributed tool methods" },
        { typeof(PrimitiveRequestTools), "request object" },
        { typeof(StructRequestTools), "request object" },
        { typeof(ArrayRequestTools), "request object" },
        { typeof(DelegateRequestTools), "request object" },
        { typeof(NoRequestTools), "exactly one request object" },
        { typeof(TwoRequestTools), "exactly one request object" },
        { typeof(CancellationOnlyTools), "exactly one request object" },
        { typeof(CancellationFirstTools), "trailing CancellationToken" },
        { typeof(TwoCancellationTools), "trailing CancellationToken" },
        { typeof(RefRequestTools), "exactly one request object" },
        { typeof(OutRequestTools), "exactly one request object" },
        { typeof(GenericMethodTools), "must not be generic" },
        { typeof(SynchronousTools), "Task<TResponse> or ValueTask<TResponse>" },
        { typeof(NonGenericTaskTools), "Task<TResponse> or ValueTask<TResponse>" },
        { typeof(NonGenericValueTaskTools), "Task<TResponse> or ValueTask<TResponse>" },
        { typeof(PrimitiveResponseTools), "response object" },
        { typeof(StructResponseTools), "response object" },
        { typeof(StringResponseTools), "response object" },
        { typeof(ArrayResponseTools), "response object" },
        { typeof(DelegateResponseTools), "response object" },
        { typeof(DuplicateTools), "duplicate tool name" }
    };

    [Theory]
    [MemberData(nameof(InvalidContracts))]
    public void Constructor_WhenContractShapeIsInvalid_RejectsBeforePublication(Type toolType, string expectedMessage)
    {
        var exception = Should.Throw<TargetInvocationException>(() => CreateContract(toolType));

        var contractFailure = exception.InnerException.ShouldBeOfType<InvalidOperationException>();
        contractFailure.Message.ShouldContain(expectedMessage);
    }

    [Fact]
    public void Constructor_WhenTaskAndValueTaskMethodsAreValid_DescribesBothInDeclarationOrder()
    {
        var contract = new McpToolContract<MultipleValidTools>();

        contract.Methods.Select(static method => method.Name.Value).ShouldBe(["first", "second"]);
        contract.Methods[0].ResponseType.ShouldBe(typeof(Response));
        contract.Methods[1].ResponseType.ShouldBe(typeof(Response));
    }

    [Fact]
    public void Constructor_WhenAttributeAndDescriptionAreInherited_CapturesOverrideMetadata()
    {
        var method = new McpToolContract<DerivedTools>().Methods.ShouldHaveSingleItem();

        method.Name.ShouldBe(new McpToolName("inherited"));
        method.Version.ShouldBe(new ToolVersion("3.4"));
        method.Description.ShouldBe("Inherited description.");
        method.Method.DeclaringType.ShouldBe(typeof(DerivedTools));
    }

    [Fact]
    public void Constructor_WhenDescriptionIsAbsent_UsesClrMethodName()
    {
        var method = new McpToolContract<NoDescriptionTools>().Methods.ShouldHaveSingleItem();

        method.Description.ShouldBe(nameof(NoDescriptionTools.ExecuteAsync));
    }

    [Fact]
    public void Constructor_WhenEffectHintsProvided_CapturesEachHint()
    {
        var method = new McpToolContract<EffectHintTools>().Methods.ShouldHaveSingleItem();

        method.ReadOnly.ShouldBeTrue();
        method.Idempotent.ShouldBeTrue();
        method.OpenWorld.ShouldBeFalse();
        method.Destructive.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenPublicMethodIsNotAttributed_IgnoresIt()
    {
        var contract = new McpToolContract<MixedTools>();

        contract.Methods.ShouldHaveSingleItem().Name.ShouldBe(new McpToolName("included"));
    }

    [Fact]
    public void Resolve_WhenMethodBelongsToContract_ReturnsStableDescriptorInstance()
    {
        var contract = new McpToolContract<NoDescriptionTools>();
        var reflected = typeof(NoDescriptionTools).GetMethod(nameof(NoDescriptionTools.ExecuteAsync))!;

        var first = contract.Resolve(reflected);
        var second = contract.Resolve(reflected);

        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void Resolve_WhenMethodIsNull_ThrowsBeforeLookup()
    {
        var contract = new McpToolContract<NoDescriptionTools>();

        var exception = Should.Throw<ArgumentNullException>(() => contract.Resolve(null!));

        exception.ParamName.ShouldBe("method");
    }

    [Fact]
    public void Resolve_WhenMethodIsNotInContract_ThrowsForMethod()
    {
        var contract = new McpToolContract<NoDescriptionTools>();
        var foreign = typeof(object).GetMethod(nameof(ToString))!;

        var exception = Should.Throw<ArgumentException>(() => contract.Resolve(foreign));

        exception.ParamName.ShouldBe("method");
        exception.Message.ShouldContain(nameof(ToString));
    }

    private static object CreateContract(Type toolType) =>
        Activator.CreateInstance(typeof(McpToolContract<>).MakeGenericType(toolType))!;

    private sealed record Request(string Value);

    private readonly record struct StructRequest(string Value);

    private sealed record Response(string Value);

    private readonly record struct StructResponse(string Value);

    private delegate Response ResponseFactory();

    private abstract class NoAttributedTools
    {
        public abstract Task<Response> ExecuteAsync(Request request);
    }

    private abstract class PrimitiveRequestTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync(int request);
    }

    private abstract class StructRequestTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync(StructRequest request);
    }

    private abstract class ArrayRequestTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync(Request[] request);
    }

    private abstract class DelegateRequestTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync(ResponseFactory request);
    }

    private abstract class NoRequestTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync();
    }

    private abstract class TwoRequestTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync(Request first, Request second);
    }

    private abstract class CancellationOnlyTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync(CancellationToken cancellationToken);
    }

#pragma warning disable CA1068 // Intentionally malformed signature verifies contract rejection.
    private abstract class CancellationFirstTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync(CancellationToken cancellationToken, Request request);
    }

    private abstract class TwoCancellationTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync(
            Request request,
            CancellationToken first,
            CancellationToken second);
    }
#pragma warning restore CA1068

    private abstract class RefRequestTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync(ref Request request);
    }

    private abstract class OutRequestTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync(out Request request);
    }

    private abstract class GenericMethodTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response> ExecuteAsync<T>(Request request);
    }

    private abstract class SynchronousTools
    {
        [McpTool("invalid", "1")]
        public abstract Response Execute(Request request);
    }

    private abstract class NonGenericTaskTools
    {
        [McpTool("invalid", "1")]
        public abstract Task ExecuteAsync(Request request);
    }

    private abstract class NonGenericValueTaskTools
    {
        [McpTool("invalid", "1")]
        public abstract ValueTask ExecuteAsync(Request request);
    }

    private abstract class PrimitiveResponseTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<int> ExecuteAsync(Request request);
    }

    private abstract class StructResponseTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<StructResponse> ExecuteAsync(Request request);
    }

    private abstract class StringResponseTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<string> ExecuteAsync(Request request);
    }

    private abstract class ArrayResponseTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<Response[]> ExecuteAsync(Request request);
    }

    private abstract class DelegateResponseTools
    {
        [McpTool("invalid", "1")]
        public abstract Task<ResponseFactory> ExecuteAsync(Request request);
    }

    private abstract class DuplicateTools
    {
        [McpTool("duplicate", "1")]
        public abstract Task<Response> FirstAsync(Request request);

        [McpTool("duplicate", "2")]
        public abstract Task<Response> SecondAsync(Request request);
    }

    private abstract class MultipleValidTools
    {
        [McpTool("first", "1")]
        public abstract Task<Response> FirstAsync(Request request, CancellationToken cancellationToken = default);

        [McpTool("second", "1")]
        public abstract ValueTask<Response> SecondAsync(Request request);
    }

    private abstract class BaseTools
    {
        [McpTool("inherited", "3.4")]
        [Description("Inherited description.")]
        public abstract Task<Response> ExecuteAsync(Request request);
    }

    private abstract class DerivedTools: BaseTools
    {
        public abstract override Task<Response> ExecuteAsync(Request request);
    }

    private abstract class NoDescriptionTools
    {
        [McpTool("fallback", "1")]
        public abstract Task<Response> ExecuteAsync(Request request);
    }

    private abstract class EffectHintTools
    {
        [McpTool("effects", "1", ReadOnly = true, Idempotent = true, OpenWorld = false, Destructive = false)]
        public abstract Task<Response> ExecuteAsync(Request request);
    }

    private abstract class MixedTools
    {
        [McpTool("included", "1")]
        public abstract Task<Response> IncludedAsync(Request request);

        public abstract Task<Response> IgnoredAsync(Request request);
    }
}
