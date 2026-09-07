// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests;

using System.Reflection;

using AgentKit;

/// <summary>
/// A reusable conformance suite proving that every AgentKit closed-hierarchy
/// leaf record whose only public constructor takes a single
/// <see cref="string"/> follows the shared "safe message" contract used
/// throughout the failure/denial vocabulary: a null or blank message is
/// rejected, and a genuine message round-trips through the type's one
/// string property unchanged.
/// </summary>
public sealed class SingleMessageLeafConformanceTests
{
    private static readonly Assembly _abstractionsAssembly = typeof(AgentId).Assembly;

    public static TheoryData<Type> SingleMessageTypes => ToTheoryData(GetTypes());

    [Fact]
    public void SingleMessageTypes_WhenDiscovered_ContainsExpectedCount() =>
        GetTypes().Count.ShouldBeGreaterThanOrEqualTo(6);

    [Theory]
    [MemberData(nameof(SingleMessageTypes))]
    public void Constructor_WhenMessageNull_ThrowsArgumentNullException(Type type)
    {
        var exception = Should.Throw<TargetInvocationException>(
            () => Activator.CreateInstance(type, [null]));

        _ = exception.InnerException.ShouldBeOfType<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(SingleMessageTypes))]
    public void Constructor_WhenMessageEmptyOrWhitespace_ThrowsArgumentException(Type type)
    {
        foreach (var invalid in new[] { string.Empty, "   " })
        {
            var exception = Should.Throw<TargetInvocationException>(
                () => Activator.CreateInstance(type, invalid));

            _ = exception.InnerException.ShouldBeOfType<ArgumentException>();
        }
    }

    [Theory]
    [MemberData(nameof(SingleMessageTypes))]
    public void Constructor_WhenMessageValid_RoundTripsThroughStringProperty(Type type)
    {
        const string message = "sample message";

        var instance = Activator.CreateInstance(type, message);

        GetMessageProperty(type).GetValue(instance).ShouldBe(message);
    }

    [Theory]
    [MemberData(nameof(SingleMessageTypes))]
    public void Equality_WhenSameMessage_InstancesAreStructurallyEqual(Type type)
    {
        var first = Activator.CreateInstance(type, "sample message");
        var second = Activator.CreateInstance(type, "sample message");

        first.ShouldBe(second);
        first!.GetHashCode().ShouldBe(second!.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(SingleMessageTypes))]
    public void Equality_WhenDifferentMessage_InstancesAreNotEqual(Type type)
    {
        var first = Activator.CreateInstance(type, "sample message one");
        var second = Activator.CreateInstance(type, "sample message two");

        first.ShouldNotBe(second);
    }

    private static PropertyInfo GetMessageProperty(Type type) =>
        Array.Find(type.GetProperties(), static p => p.PropertyType == typeof(string))
        ?? throw new InvalidOperationException($"{type} does not declare a string property.");

    private static IReadOnlyList<Type> GetTypes() =>
        [.. _abstractionsAssembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false, Namespace: "AgentKit", IsGenericTypeDefinition: false }
                && !typeof(Exception).IsAssignableFrom(type)
                && !_excludedTypeNames.Contains(type.Name)
                && HasSingleStringConstructor(type))];

    // Content-delta leaves intentionally allow an empty (but not null)
    // fragment, since a real streaming increment can legitimately be
    // empty; they use ArgumentNullException.ThrowIfNull rather than the
    // "safe message" ThrowIfNullOrWhiteSpace contract this suite checks.
    //
    // StaticApiKeyCredentialSource is a credential-holding service adapter,
    // not a message value type: its single-string constructor happens to
    // match this suite's discovery shape, but it deliberately never
    // exposes the secret through a public string property (so
    // GetMessageProperty would fail to find one) and deliberately uses
    // reference identity rather than record-style structural equality (so
    // two instances constructed with the same key are intentionally not
    // "equal"), because secret material must never be casually comparable
    // or reflectively enumerable the way an ordinary safe message is.
    private static readonly HashSet<string> _excludedTypeNames =
    [
        nameof(TextContentDelta),
        nameof(StructuredDataContentDelta),
        nameof(StaticApiKeyCredentialSource)
    ];

    private static bool HasSingleStringConstructor(Type type) =>
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Any(constructor => constructor.GetParameters() is [{ ParameterType.Name: nameof(String) }]);

    private static TheoryData<Type> ToTheoryData(IEnumerable<Type> types)
    {
        var data = new TheoryData<Type>();
        foreach (var type in types)
        {
            data.Add(type);
        }

        return data;
    }
}
