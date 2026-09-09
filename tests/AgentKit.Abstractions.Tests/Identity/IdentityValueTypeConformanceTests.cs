// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using System.Globalization;
using System.Reflection;

using AgentKit;

/// <summary>
/// A reusable conformance suite proving that every AgentKit identity value
/// type follows the shared construction-validation contract described in
/// <c>docs/architecture/composition-and-configuration.md#typed-identity</c>:
/// a default/empty value is rejected by the constructor, and a genuine value
/// round-trips through <c>Value</c> unchanged. Running this once via
/// reflection, instead of duplicating near-identical tests per type, is what
/// keeps this suite complete as new identity types are added.
/// </summary>
public sealed class IdentityValueTypeConformanceTests
{
    private static readonly Assembly _abstractionsAssembly = typeof(AgentId).Assembly;

    // NetworkMethod canonicalizes to uppercase and NetworkRoute requires a
    // leading '/', so neither round-trips an arbitrary non-whitespace
    // string unchanged the way every other string-backed identity in this
    // suite does. OutputValidatorReference deliberately exposes its
    // underlying text as "Name" rather than "Value" to stay symmetric with
    // the "Name" property IOutputValidator itself already declares for the
    // same concept, rather than mixing both conventions for one idea. All
    // three still have their own focused, type-specific tests.
    private static readonly HashSet<string> _excludedStringBackedTypeNames =
        [nameof(NetworkMethod), nameof(NetworkRoute), nameof(OutputValidatorReference)];

    public static TheoryData<Type> GuidBackedIdentityTypes => ToTheoryData(GetIdentityTypes(typeof(Guid)));

    public static TheoryData<Type> StringBackedIdentityTypes =>
        ToTheoryData(GetIdentityTypes(typeof(string)).Where(type => !_excludedStringBackedTypeNames.Contains(type.Name)));

    public static TheoryData<Type> LongBackedOrderingTypes => ToTheoryData(GetIdentityTypes(typeof(long)));

    [Fact]
    public void GuidBackedIdentityTypes_WhenDiscovered_ContainsExpectedCount() =>
        // Guards against the reflection query silently matching nothing (for
        // example after a refactor) and the theories below passing vacuously.
        GetIdentityTypes(typeof(Guid)).Count.ShouldBeGreaterThanOrEqualTo(13);

    [Fact]
    public void StringBackedIdentityTypes_WhenDiscovered_ContainsExpectedCount() => GetIdentityTypes(typeof(string)).Count.ShouldBeGreaterThanOrEqualTo(20);

    [Fact]
    public void LongBackedOrderingTypes_WhenDiscovered_ContainsExpectedCount() => GetIdentityTypes(typeof(long)).Count.ShouldBeGreaterThanOrEqualTo(2);

    [Theory]
    [MemberData(nameof(GuidBackedIdentityTypes))]
    public void Constructor_WhenGuidIsEmpty_ThrowsArgumentOutOfRangeException(Type identityType)
    {
        var exception = Should.Throw<TargetInvocationException>(
            () => Activator.CreateInstance(identityType, Guid.Empty));

        _ = exception.InnerException.ShouldBeOfType<ArgumentOutOfRangeException>();
    }

    [Theory]
    [MemberData(nameof(GuidBackedIdentityTypes))]
    public void Constructor_WhenGuidIsNonEmpty_RoundTripsThroughValue(Type identityType)
    {
        var guid = Guid.NewGuid();

        var instance = Activator.CreateInstance(identityType, guid);

        GetValueProperty(identityType).GetValue(instance).ShouldBe(guid);
    }

    [Theory]
    [MemberData(nameof(StringBackedIdentityTypes))]
    public void Constructor_WhenStringIsNull_ThrowsArgumentNullException(Type identityType)
    {
        // ArgumentException.ThrowIfNullOrWhiteSpace, which every
        // string-backed identity constructor delegates to, throws the more
        // specific ArgumentNullException for a null argument and the base
        // ArgumentException only for empty/whitespace text.
        var exception = Should.Throw<TargetInvocationException>(
            () => Activator.CreateInstance(identityType, [null]));

        _ = exception.InnerException.ShouldBeOfType<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(StringBackedIdentityTypes))]
    public void Constructor_WhenStringIsEmptyOrWhitespace_ThrowsArgumentException(Type identityType)
    {
        foreach (var invalid in new[] { string.Empty, "   " })
        {
            var exception = Should.Throw<TargetInvocationException>(
                () => Activator.CreateInstance(identityType, invalid));

            _ = exception.InnerException.ShouldBeOfType<ArgumentException>();
        }
    }

    [Theory]
    [MemberData(nameof(StringBackedIdentityTypes))]
    public void Constructor_WhenStringIsValid_RoundTripsThroughValue(Type identityType)
    {
        const string value = "sample-value";

        var instance = Activator.CreateInstance(identityType, value);

        GetValueProperty(identityType).GetValue(instance).ShouldBe(value);
    }

    [Theory]
    [MemberData(nameof(LongBackedOrderingTypes))]
    public void Constructor_WhenLongIsNegative_ThrowsArgumentOutOfRangeException(Type identityType)
    {
        var exception = Should.Throw<TargetInvocationException>(
            () => Activator.CreateInstance(identityType, -1L));

        _ = exception.InnerException.ShouldBeOfType<ArgumentOutOfRangeException>();
    }

    [Theory]
    [MemberData(nameof(LongBackedOrderingTypes))]
    public void Constructor_WhenLongIsWithinDeclaredDomain_RoundTripsOrRejectsZero(Type identityType)
    {
        foreach (var value in new[] { 0L, 42L })
        {
            if (value == 0 && RequiresPositiveValue(identityType))
            {
                var exception = Should.Throw<TargetInvocationException>(
                    () => Activator.CreateInstance(identityType, value));
                _ = exception.InnerException.ShouldBeOfType<ArgumentOutOfRangeException>();
            }
            else
            {
                var instance = Activator.CreateInstance(identityType, value);
                GetValueProperty(identityType).GetValue(instance).ShouldBe(value);
            }
        }
    }

    [Theory]
    [MemberData(nameof(GuidBackedIdentityTypes))]
    [MemberData(nameof(StringBackedIdentityTypes))]
    [MemberData(nameof(LongBackedOrderingTypes))]
    public void Equality_WhenValuesMatch_InstancesAreStructurallyEqual(Type identityType)
    {
        var value = SampleValueFor(identityType, 1);

        var first = Activator.CreateInstance(identityType, value);
        var second = Activator.CreateInstance(identityType, value);

        first.ShouldBe(second);
        first!.GetHashCode().ShouldBe(second!.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(GuidBackedIdentityTypes))]
    public void ToString_WhenGuidBacked_ReturnsCanonicalGuidFormat(Type identityType)
    {
        var guid = Guid.NewGuid();
        var instance = Activator.CreateInstance(identityType, guid);

        instance!.ToString().ShouldBe(guid.ToString("D"));
    }

    [Theory]
    [MemberData(nameof(StringBackedIdentityTypes))]
    public void ToString_WhenStringBacked_ReturnsUnderlyingText(Type identityType)
    {
        const string value = "sample-value";
        var instance = Activator.CreateInstance(identityType, value);

        instance!.ToString().ShouldBe(value);
    }

    [Theory]
    [MemberData(nameof(LongBackedOrderingTypes))]
    public void ToString_WhenLongBacked_ReturnsInvariantCultureText(Type identityType)
    {
        const long value = 42L;
        var instance = Activator.CreateInstance(identityType, value);

        instance!.ToString().ShouldBe(value.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [MemberData(nameof(GuidBackedIdentityTypes))]
    [MemberData(nameof(StringBackedIdentityTypes))]
    [MemberData(nameof(LongBackedOrderingTypes))]
    public void Equality_WhenValuesDiffer_InstancesAreNotEqual(Type identityType)
    {
        var first = Activator.CreateInstance(identityType, SampleValueFor(identityType, 1));
        var second = Activator.CreateInstance(identityType, SampleValueFor(identityType, 2));

        first.ShouldNotBe(second);
    }

    private static object SampleValueFor(Type identityType, int variant)
    {
        var valueType = GetValueProperty(identityType).PropertyType;

        return valueType switch
        {
            _ when valueType == typeof(Guid) => Guid.NewGuid(),
            _ when valueType == typeof(string) => $"sample-value-{variant}",
            _ when valueType == typeof(long) => 7L + variant,
            _ => throw new NotSupportedException($"Unsupported identity value type: {valueType}."),
        };
    }

    private static PropertyInfo GetValueProperty(Type identityType) =>
        identityType.GetProperty("Value")
        ?? throw new InvalidOperationException($"{identityType} does not declare a Value property.");

    private static IReadOnlyList<Type> GetIdentityTypes(Type parameterType) =>
        [.. _abstractionsAssembly
            .GetTypes()
            .Where(type =>
                type is { IsValueType: true, Namespace: "AgentKit", IsGenericTypeDefinition: false }
                && HasSingleParameterPublicConstructor(type, parameterType))];

    private static bool HasSingleParameterPublicConstructor(Type type, Type parameterType) =>
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Any(constructor =>
                constructor.GetParameters() is [{ ParameterType: var actual }]
                && actual == parameterType);

    private static bool RequiresPositiveValue(Type identityType) =>
        identityType == typeof(SecurityPolicyVersion)
        || identityType == typeof(SecurityRevocationVersion)
        || identityType == typeof(FencingToken)
        || identityType == typeof(IdentityVersion)
        || identityType == typeof(PlanRevision)
        || identityType == typeof(ArtifactProfileVersion)
        || identityType == typeof(BudgetProfileVersion)
        || identityType == typeof(BudgetAccountingRevision)
        || identityType == typeof(OperationStateRevision)
        || identityType == typeof(SessionLaneRevision)
        || identityType == typeof(SessionDirectoryRevision)
        || identityType == typeof(SessionProfileVersion)
        || identityType == typeof(OutputSchemaProfileVersion)
        || identityType == typeof(RunPolicyVersion)
        || identityType == typeof(SecurityProfileVersion)
        || identityType == typeof(ConfigurationVersion)
        || identityType == typeof(BudgetLedgerWatermark)
        || identityType == typeof(ToolExecutionPolicyVersion)
        || identityType == typeof(ToolResultProjectionPolicyVersion)
        || identityType == typeof(ConfigurationSourceVersion);

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
