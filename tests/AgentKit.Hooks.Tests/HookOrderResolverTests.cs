// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

/// <summary>Verifies <see cref="HookOrderResolver"/> against the hook ordering rules.</summary>
public sealed class HookOrderResolverTests
{
    private static readonly HookPointId Point = new("agent.run-started");

    private static readonly HookProfileKey Profile = new("default");

    private static readonly HookRegistrationId Alpha = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private static readonly HookRegistrationId Bravo = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private static readonly HookRegistrationId Charlie = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));

    private static readonly HookRegistrationId Missing = new(Guid.Parse("99999999-9999-9999-9999-999999999999"));

    private readonly HookOrderResolver _resolver = new();

    [Fact]
    public void Resolve_WhenRegistrationsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => _ = _resolver.Resolve(default));

        exception.ParamName.ShouldBe("registrations");
    }

    [Fact]
    public void Resolve_WhenRegistrationsContainNull_ThrowsArgumentException()
    {
        ImmutableArray<HookRegistrationDescriptor> registrations = [null!];

        var exception = Should.Throw<ArgumentException>(() => _ = _resolver.Resolve(registrations));

        exception.ParamName.ShouldBe("registrations");
    }

    [Fact]
    public void Resolve_WhenEmpty_ReturnsEmptyOrder()
    {
        var resolved = _resolver.Resolve([]).ShouldBeOfType<HookOrderResolved>();

        resolved.Ordered.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_WhenUnconstrained_PreservesDiscoveryOrder()
    {
        var first = Descriptor(Alpha);
        var second = Descriptor(Bravo);
        var third = Descriptor(Charlie);

        ShouldOrder(_resolver.Resolve([first, second, third]), first, second, third);
    }

    [Fact]
    public void Resolve_WhenBeforeTargetsPresent_RunsEarlier()
    {
        var alpha = Descriptor(Alpha);
        var bravo = Descriptor(Bravo);
        var charlie = Descriptor(Charlie, before: [Alpha, Bravo]);

        ShouldOrder(_resolver.Resolve([alpha, bravo, charlie]), charlie, alpha, bravo);
    }

    [Fact]
    public void Resolve_WhenBeforeTargetsOneRegistration_PreservesEarlierUnrelatedRegistration()
    {
        var alpha = Descriptor(Alpha);
        var bravo = Descriptor(Bravo);
        var charlie = Descriptor(Charlie, before: [Alpha]);

        ShouldOrder(_resolver.Resolve([alpha, bravo, charlie]), bravo, charlie, alpha);
    }

    [Fact]
    public void Resolve_WhenBeforeTargetMissing_IgnoresConstraint()
    {
        var alpha = Descriptor(Alpha, before: [Missing]);
        var bravo = Descriptor(Bravo);

        ShouldOrder(_resolver.Resolve([alpha, bravo]), alpha, bravo);
    }

    [Fact]
    public void Resolve_WhenAfterTargetsPresent_RunsLater()
    {
        var alpha = Descriptor(Alpha, after: [Charlie]);
        var bravo = Descriptor(Bravo);
        var charlie = Descriptor(Charlie);

        ShouldOrder(_resolver.Resolve([alpha, bravo, charlie]), bravo, charlie, alpha);
    }

    [Fact]
    public void Resolve_WhenDependsOnPresent_RunsAfterDependency()
    {
        var alpha = Descriptor(Alpha, dependsOn: [Bravo]);
        var bravo = Descriptor(Bravo);

        ShouldOrder(_resolver.Resolve([alpha, bravo]), bravo, alpha);
    }

    [Fact]
    public void Resolve_WhenDependsOnMissing_ReturnsMissingDependency()
    {
        var codes = Codes(_resolver.Resolve([Descriptor(Alpha, dependsOn: [Missing])]));

        codes.ShouldBe([HookOrderDiagnosticCodes.MissingDependency]);
    }

    [Fact]
    public void Resolve_WhenFirstDeclared_RunsFirst()
    {
        var alpha = Descriptor(Alpha);
        var bravo = Descriptor(Bravo);
        var charlie = Descriptor(Charlie, HookOrder.First);

        ShouldOrder(_resolver.Resolve([alpha, bravo, charlie]), charlie, alpha, bravo);
    }

    [Fact]
    public void Resolve_WhenLastDeclared_RunsLast()
    {
        var charlie = Descriptor(Charlie, HookOrder.Last);
        var alpha = Descriptor(Alpha);
        var bravo = Descriptor(Bravo);

        ShouldOrder(_resolver.Resolve([charlie, alpha, bravo]), alpha, bravo, charlie);
    }

    [Fact]
    public void Resolve_WhenFirstAndLastDeclared_PlacesAnchorsAtEnds()
    {
        var bravo = Descriptor(Bravo);
        var last = Descriptor(Charlie, HookOrder.Last);
        var alpha = Descriptor(Alpha);
        var first = Descriptor(Missing, HookOrder.First);

        ShouldOrder(_resolver.Resolve([bravo, last, alpha, first]), first, bravo, alpha, last);
    }

    [Fact]
    public void Resolve_WhenFirstAfterTargetMissing_IgnoresConstraint()
    {
        var first = Descriptor(Alpha, HookOrder.First, after: [Missing]);
        var bravo = Descriptor(Bravo);

        ShouldOrder(_resolver.Resolve([bravo, first]), first, bravo);
    }

    [Fact]
    public void Resolve_WhenTwoFirstAnchors_ReturnsMultipleFirst()
    {
        var codes = Codes(_resolver.Resolve([
            Descriptor(Alpha, HookOrder.First),
            Descriptor(Bravo, HookOrder.First),
        ]));

        codes.ShouldBe([HookOrderDiagnosticCodes.MultipleFirst]);
    }

    [Fact]
    public void Resolve_WhenTwoLastAnchors_ReturnsMultipleLast()
    {
        var codes = Codes(_resolver.Resolve([
            Descriptor(Alpha, HookOrder.Last),
            Descriptor(Bravo, HookOrder.Last),
        ]));

        codes.ShouldBe([HookOrderDiagnosticCodes.MultipleLast]);
    }

    [Fact]
    public void Resolve_WhenDuplicateRegistration_ReturnsDuplicateRegistration()
    {
        var invalid = _resolver.Resolve([Descriptor(Alpha), Descriptor(Alpha)]).ShouldBeOfType<HookOrderInvalid>();

        invalid.Diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe([HookOrderDiagnosticCodes.DuplicateRegistration]);
        invalid.Diagnostics[0].SafeMessage.ShouldContain(Alpha.ToString());
    }

    [Fact]
    public void Resolve_WhenSelfReferenced_ReturnsSelfReference()
    {
        var codes = Codes(_resolver.Resolve([Descriptor(Alpha, before: [Alpha])]));

        codes.ShouldBe([HookOrderDiagnosticCodes.SelfReference]);
    }

    [Fact]
    public void Resolve_WhenBeforeAndAfterNameSameTarget_ReturnsContradictoryEdges()
    {
        var codes = Codes(_resolver.Resolve([
            Descriptor(Alpha, before: [Bravo], after: [Bravo]),
            Descriptor(Bravo),
        ]));

        codes.ShouldBe([HookOrderDiagnosticCodes.ContradictoryEdges]);
    }

    [Fact]
    public void Resolve_WhenBeforeAndDependsOnNameSameTarget_ReturnsContradictoryEdges()
    {
        var codes = Codes(_resolver.Resolve([
            Descriptor(Alpha, before: [Bravo], dependsOn: [Bravo]),
            Descriptor(Bravo),
        ]));

        codes.ShouldBe([HookOrderDiagnosticCodes.ContradictoryEdges]);
    }

    [Fact]
    public void Resolve_WhenMutualAfter_ReturnsCycle()
    {
        var codes = Codes(_resolver.Resolve([
            Descriptor(Alpha, after: [Bravo]),
            Descriptor(Bravo, after: [Alpha]),
        ]));

        codes.ShouldBe([HookOrderDiagnosticCodes.Cycle]);
    }

    [Fact]
    public void Resolve_WhenFirstDependsOnAnother_ReturnsAnchorContradictionAndCycle()
    {
        var codes = Codes(_resolver.Resolve([
            Descriptor(Alpha, HookOrder.First, dependsOn: [Bravo]),
            Descriptor(Bravo),
        ]));

        codes.ShouldBe([HookOrderDiagnosticCodes.AnchorContradiction, HookOrderDiagnosticCodes.Cycle]);
    }

    [Fact]
    public void Resolve_WhenRegistrationRunsBeforeFirst_ReturnsAnchorContradictionAndCycle()
    {
        var codes = Codes(_resolver.Resolve([
            Descriptor(Alpha, HookOrder.First),
            Descriptor(Bravo, before: [Alpha]),
        ]));

        codes.ShouldBe([HookOrderDiagnosticCodes.AnchorContradiction, HookOrderDiagnosticCodes.Cycle]);
    }

    [Fact]
    public void Resolve_WhenLastRunsBeforeAnother_ReturnsAnchorContradictionAndCycle()
    {
        var codes = Codes(_resolver.Resolve([
            Descriptor(Alpha, HookOrder.Last, before: [Bravo]),
            Descriptor(Bravo),
        ]));

        codes.ShouldBe([HookOrderDiagnosticCodes.AnchorContradiction, HookOrderDiagnosticCodes.Cycle]);
    }

    [Fact]
    public void Resolve_WhenRegistrationDependsOnLast_ReturnsAnchorContradictionAndCycle()
    {
        var codes = Codes(_resolver.Resolve([
            Descriptor(Alpha, HookOrder.Last),
            Descriptor(Bravo, dependsOn: [Alpha]),
        ]));

        codes.ShouldBe([HookOrderDiagnosticCodes.AnchorContradiction, HookOrderDiagnosticCodes.Cycle]);
    }

    [Fact]
    public void Resolve_WhenFirstDependsOnMissing_ReturnsMissingDependencyAndAnchorContradiction()
    {
        var codes = Codes(_resolver.Resolve([Descriptor(Alpha, HookOrder.First, dependsOn: [Missing])]));

        codes.ShouldBe([HookOrderDiagnosticCodes.MissingDependency, HookOrderDiagnosticCodes.AnchorContradiction]);
    }

    [Fact]
    public void Resolve_WhenDuplicateAndMissingDependency_ReturnsEveryDiagnostic()
    {
        var codes = Codes(_resolver.Resolve([
            Descriptor(Alpha, dependsOn: [Missing]),
            Descriptor(Alpha),
        ]));

        codes.ShouldBe([HookOrderDiagnosticCodes.DuplicateRegistration, HookOrderDiagnosticCodes.MissingDependency]);
    }

    [Fact]
    public void Resolve_WhenDefaultEdgeArrays_PreservesDiscoveryOrder()
    {
        var alpha = new HookRegistrationDescriptor(
            Alpha,
            Point,
            Profile,
            HookOrder.Normal,
            HookLifetime.Singleton,
            HookFailureMode.FailOperation,
            HookReentrancyPolicy.Forbidden,
            default,
            default,
            default);
        var bravo = Descriptor(Bravo);

        ShouldOrder(_resolver.Resolve([alpha, bravo]), alpha, bravo);
    }

    [Fact]
    public void Resolve_WhenCalledTwice_ReturnsEqualOrder()
    {
        var registrations = ImmutableArray.Create(Descriptor(Alpha), Descriptor(Bravo, HookOrder.First));

        var first = _resolver.Resolve(registrations).ShouldBeOfType<HookOrderResolved>();
        var second = _resolver.Resolve(registrations).ShouldBeOfType<HookOrderResolved>();

        first.Ordered.Select(static registration => registration.Id).ShouldBe(second.Ordered.Select(static registration => registration.Id));
    }

    private static void ShouldOrder(
        HookOrderResult result,
        params HookRegistrationDescriptor[] expected)
    {
        var resolved = result.ShouldBeOfType<HookOrderResolved>();
        resolved.Ordered.Select(static registration => registration.Id).ShouldBe(expected.Select(static registration => registration.Id));
        for (var i = 0; i < expected.Length; i++)
        {
            resolved.Ordered[i].ShouldBeSameAs(expected[i]);
        }
    }

    private static string[] Codes(HookOrderResult result)
    {
        var invalid = result.ShouldBeOfType<HookOrderInvalid>();
        return [.. invalid.Diagnostics.Select(static diagnostic => diagnostic.Code)];
    }

    private static HookRegistrationDescriptor Descriptor(
        HookRegistrationId id,
        HookOrder? order = null,
        ImmutableArray<HookRegistrationId> before = default,
        ImmutableArray<HookRegistrationId> after = default,
        ImmutableArray<HookRegistrationId> dependsOn = default) =>
        new(
            id,
            Point,
            Profile,
            order ?? HookOrder.Normal,
            HookLifetime.Singleton,
            HookFailureMode.FailOperation,
            HookReentrancyPolicy.Forbidden,
            before.IsDefault ? [] : before,
            after.IsDefault ? [] : after,
            dependsOn.IsDefault ? [] : dependsOn);
}
