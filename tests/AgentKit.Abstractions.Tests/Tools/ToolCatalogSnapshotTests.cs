// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

public sealed class ToolCatalogSnapshotTests
{
    [Fact]
    public void Constructor_WhenGraphValid_CapturesEveryFieldAndPreservesToolOrder()
    {
        var first = Descriptor("read", "1");
        var second = Descriptor("write", "2");
        var snapshot = Create(
            [first, second],
            Policies(first, second),
            ImmutableDictionary.CreateRange(new Dictionary<ToolAlias, ToolIdentity>
            {
                [new ToolAlias("read_file")] = Identity(first),
                [new ToolAlias("read_legacy")] = Identity(first),
            }));

        snapshot.AgentId.ShouldBe(Agent());
        snapshot.SessionId.ShouldBe(Session());
        snapshot.RunId.ShouldBe(Run());
        snapshot.AgentDefinitionRevision.ShouldBe(new AgentDefinitionRevision(0));
        snapshot.ConfigurationVersion.ShouldBe(new ConfigurationVersion(4));
        snapshot.Version.ShouldBe(new ToolCatalogVersion("catalog-7"));
        snapshot.Tools.ShouldBe([first, second]);
        snapshot.ExecutionPolicies.Keys.ShouldBe([Identity(first), Identity(second)], ignoreOrder: true);
        snapshot.ProviderAliases.Count.ShouldBe(2);
        snapshot.ProviderAliases.Values.Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public void Constructor_WhenCatalogEmpty_AcceptsInitializedEmptyCollections()
    {
        var snapshot = Create([], EmptyPolicies(),
            EmptyAliases());

        snapshot.Tools.ShouldBeEmpty();
        snapshot.ExecutionPolicies.ShouldBeEmpty();
        snapshot.ProviderAliases.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenScalarOrReferenceInvalid_ThrowsExactException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreateInvalid(InvalidField.AgentId)).ParamName.ShouldBe("agentId");
        Should.Throw<ArgumentOutOfRangeException>(() => CreateInvalid(InvalidField.SessionId)).ParamName.ShouldBe("sessionId");
        Should.Throw<ArgumentOutOfRangeException>(() => CreateInvalid(InvalidField.RunId)).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentNullException>(() => CreateInvalid(InvalidField.Identity)).ParamName.ShouldBe("identity");
        Should.Throw<ArgumentNullException>(() => CreateInvalid(InvalidField.SecurityPolicy)).ParamName.ShouldBe("securityPolicy");
        Should.Throw<ArgumentOutOfRangeException>(() => CreateInvalid(InvalidField.ConfigurationVersion)).ParamName.ShouldBe("configurationVersion");
        Should.Throw<ArgumentOutOfRangeException>(() => CreateInvalid(InvalidField.Version)).ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenCollectionsInvalid_ThrowsExactException()
    {
        Should.Throw<ArgumentException>(() => CreateInvalid(InvalidField.Tools)).ParamName.ShouldBe("tools");
        Should.Throw<ArgumentException>(() => Create(tools: [null!])).ParamName.ShouldBe("tools");
        Should.Throw<ArgumentNullException>(() => CreateInvalid(InvalidField.ExecutionPolicies)).ParamName.ShouldBe("executionPolicies");
        Should.Throw<ArgumentNullException>(() => CreateInvalid(InvalidField.ProviderAliases)).ParamName.ShouldBe("providerAliases");
    }

    [Fact]
    public void Constructor_WhenDescriptorIdentityDuplicated_ThrowsOwningParameter()
    {
        var first = Descriptor("read", "1");
        var duplicate = Descriptor("read", "1", name: "other");

        Should.Throw<ArgumentException>(() => Create([first, duplicate], Policies(first), EmptyAliases())).ParamName.ShouldBe("tools");
    }

    [Fact]
    public void Constructor_WhenPolicyGraphNotExact_ThrowsOwningParameter()
    {
        var tool = Descriptor("read", "1");
        var orphan = Descriptor("orphan", "1");

        Should.Throw<ArgumentException>(() => Create([tool], EmptyPolicies(), EmptyAliases())).ParamName.ShouldBe("executionPolicies");
        Should.Throw<ArgumentException>(() => Create([tool], Policies(tool, orphan), EmptyAliases())).ParamName.ShouldBe("executionPolicies");
        Should.Throw<ArgumentException>(() => Create([tool], Policies(orphan), EmptyAliases())).ParamName.ShouldBe("executionPolicies");
        Should.Throw<ArgumentOutOfRangeException>(() => Create([tool], ImmutableDictionary.CreateRange(new Dictionary<ToolIdentity, ToolExecutionPolicyReference>
        {
            [default] = Policy(),
        }), EmptyAliases())).ParamName.ShouldBe("executionPolicies");
        Should.Throw<ArgumentNullException>(() => Create([tool], ImmutableDictionary.CreateRange(new Dictionary<ToolIdentity, ToolExecutionPolicyReference>
        {
            [Identity(tool)] = null!,
        }), EmptyAliases())).ParamName.ShouldBe("executionPolicies");
    }

    [Fact]
    public void Constructor_WhenAliasInvalidOrDangling_ThrowsOwningParameter()
    {
        var tool = Descriptor("read", "1");
        var policies = Policies(tool);

        Should.Throw<ArgumentOutOfRangeException>(() => Create([tool], policies,
            ImmutableDictionary.CreateRange(new Dictionary<ToolAlias, ToolIdentity> { [default] = Identity(tool) }))).ParamName.ShouldBe("providerAliases");
        Should.Throw<ArgumentOutOfRangeException>(() => Create([tool], policies,
            ImmutableDictionary.CreateRange(new Dictionary<ToolAlias, ToolIdentity> { [new ToolAlias("read")] = default }))).ParamName.ShouldBe("providerAliases");
        Should.Throw<ArgumentException>(() => Create([tool], policies,
            ImmutableDictionary.CreateRange(new Dictionary<ToolAlias, ToolIdentity> { [new ToolAlias("other")] = new(new ToolId("other"), new ToolVersion("1")) }))).ParamName.ShouldBe("providerAliases");
    }

    [Fact]
    public void Constructor_WhenInputUsesCustomComparers_NormalizesKeyAndValueComparers()
    {
        var tool = Descriptor("read", "1");
        var aliases = ImmutableDictionary.Create<ToolAlias, ToolIdentity>(new AliasIgnoreCaseComparer())
            .Add(new ToolAlias("Read"), Identity(tool));
        var policies = ImmutableDictionary.Create(
            EqualityComparer<ToolIdentity>.Default, new AlwaysEqualPolicyComparer()).Add(Identity(tool), Policy());

        var snapshot = Create([tool], policies, aliases);

        snapshot.ProviderAliases.KeyComparer.ShouldBe(EqualityComparer<ToolAlias>.Default);
        snapshot.ProviderAliases.ContainsKey(new ToolAlias("read")).ShouldBeFalse();
        snapshot.ExecutionPolicies.ValueComparer.ShouldBe(EqualityComparer<ToolExecutionPolicyReference>.Default);
    }

    [Fact]
    public void Constructor_WhenNormalizationExposesCollision_ThrowsOwningParameter()
    {
        var tool = Descriptor("read", "1");
        var aliases = ImmutableDictionary.Create<ToolAlias, ToolIdentity>(new NeverEqualAliasComparer())
            .Add(new ToolAlias("read"), Identity(tool))
            .Add(new ToolAlias("read"), Identity(tool));

        Should.Throw<ArgumentException>(() => Create([tool], Policies(tool), aliases)).ParamName.ShouldBe("providerAliases");
    }

    [Fact]
    public void Constructor_WhenPolicyNormalizationExposesCollision_ThrowsOwningParameter()
    {
        var tool = Descriptor("read", "1");
        var identity = Identity(tool);
        var policies = ImmutableDictionary.Create<ToolIdentity, ToolExecutionPolicyReference>(new NeverEqualIdentityComparer())
            .Add(identity, Policy())
            .Add(identity, Policy());

        Should.Throw<ArgumentException>(() => Create([tool], policies, EmptyAliases())).ParamName.ShouldBe("executionPolicies");
    }

    [Fact]
    public void Equality_WhenMapOrderAndInputComparersDiffer_IsSymmetricAndHashCompatible()
    {
        var first = Descriptor("read", "1");
        var second = Descriptor("write", "2");
        var left = Create([first, second], Policies(first, second), ImmutableDictionary.CreateRange(new Dictionary<ToolAlias, ToolIdentity>
        {
            [new ToolAlias("read")] = Identity(first),
            [new ToolAlias("write")] = Identity(second),
        }));
        var right = Create([first, second], Policies(second, first), ImmutableDictionary.CreateRange(new Dictionary<ToolAlias, ToolIdentity>
        {
            [new ToolAlias("write")] = Identity(second),
            [new ToolAlias("read")] = Identity(first),
        }));

        left.ShouldBe(right);
        right.ShouldBe(left);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        Create([second, first], Policies(first, second), right.ProviderAliases).ShouldNotBe(left);
    }

    [Fact]
    public void Equality_WhenBoundScalarPolicyOrAliasDiffers_ReturnsFalse()
    {
        var tool = Descriptor("read", "1");
        var aliases = ImmutableDictionary<ToolAlias, ToolIdentity>.Empty.Add(new ToolAlias("read"), Identity(tool));
        var baseline = Create([tool], Policies(tool), aliases);
        var changedPolicy = ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference>.Empty.Add(
            Identity(tool), new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("strict"), new ToolExecutionPolicyVersion(2)));
        var changedAlias = ImmutableDictionary<ToolAlias, ToolIdentity>.Empty.Add(new ToolAlias("read_file"), Identity(tool));

        Create([tool], Policies(tool), aliases, version: new ToolCatalogVersion("catalog-8")).ShouldNotBe(baseline);
        Create([tool], changedPolicy, aliases).ShouldNotBe(baseline);
        Create([tool], Policies(tool), changedAlias).ShouldNotBe(baseline);
    }

    [Fact]
    public void Constructor_WhenSourcePublicationIsMissingOrInvalid_RejectsExactParameter()
    {
        var tool = Descriptor("read", "1");
        var nullMap = Should.Throw<ArgumentNullException>(() => CreateInvalid(InvalidField.SourceVersions));
        nullMap.GetType().ShouldBe(typeof(ArgumentNullException));
        nullMap.ParamName.ShouldBe("sourceVersions");
        var absent = Should.Throw<ArgumentException>(() => Create([tool], Policies(tool), sourceVersions: []));
        absent.GetType().ShouldBe(typeof(ArgumentException));
        absent.ParamName.ShouldBe("sourceVersions");
        var defaultSource = Should.Throw<ArgumentOutOfRangeException>(() => Create(sourceVersions:
            ImmutableDictionary<ToolSourceId, ToolSourceVersion>.Empty.Add(default, new("source-7"))));
        defaultSource.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        defaultSource.ParamName.ShouldBe("sourceVersions");
        var defaultVersion = Should.Throw<ArgumentOutOfRangeException>(() => Create(sourceVersions:
            ImmutableDictionary<ToolSourceId, ToolSourceVersion>.Empty.Add(new("source"), default)));
        defaultVersion.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        defaultVersion.ParamName.ShouldBe("sourceVersions");
    }

    [Fact]
    public void Constructor_WhenSelectedSourceExposesNoTools_RetainsItsExactVersion()
    {
        var sources = Sources().Add(new ToolSourceId("empty-source"), new ToolSourceVersion("empty-v3"));
        var snapshot = Create(sourceVersions: sources);
        snapshot.SourceVersions.ShouldBe(sources);
        snapshot.Tools.ShouldBeEmpty();
        Create(sourceVersions: []).SourceVersions.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenSourceComparersWeakenIdentity_NormalizesThemWithoutChangingSpelling()
    {
        var keys = EqualityComparer<ToolSourceId>.Create(
            static (left, right) => StringComparer.OrdinalIgnoreCase.Equals(left.Value, right.Value),
            static source => StringComparer.OrdinalIgnoreCase.GetHashCode(source.Value));
        var values = EqualityComparer<ToolSourceVersion>.Create(static (_, _) => true, static _ => 0);
        var sources = ImmutableDictionary.Create(keys, values)
            .Add(new ToolSourceId("Source"), new ToolSourceVersion("v1"));
        var snapshot = Create(sourceVersions: sources);
        snapshot.SourceVersions.KeyComparer.ShouldBe(EqualityComparer<ToolSourceId>.Default);
        snapshot.SourceVersions.ValueComparer.ShouldBe(EqualityComparer<ToolSourceVersion>.Default);
        snapshot.SourceVersions.ContainsKey(new ToolSourceId("source")).ShouldBeFalse();
        snapshot.SourceVersions[new ToolSourceId("Source")].ShouldBe(new ToolSourceVersion("v1"));
    }

    [Fact]
    public void Constructor_WhenSourceNormalizationRevealsDuplicates_RejectsInsteadOfChoosingRegistrationOrder()
    {
        var comparer = EqualityComparer<ToolSourceId>.Create(static (_, _) => false, static source => source.GetHashCode());
        var sources = ImmutableDictionary.Create<ToolSourceId, ToolSourceVersion>(comparer)
            .Add(new ToolSourceId("source"), new ToolSourceVersion("first"))
            .Add(new ToolSourceId("source"), new ToolSourceVersion("second"));
        var error = Should.Throw<ArgumentException>(() => Create(sourceVersions: sources));
        error.GetType().ShouldBe(typeof(ArgumentException));
        error.ParamName.ShouldBe("sourceVersions");
    }

    [Fact]
    public void Equals_WhenSourcePublicationChanges_DistinguishesSameDescriptorsAndCatalogVersion()
    {
        var tool = Descriptor("read", "1");
        var first = Create([tool], Policies(tool));
        var equal = Create([Descriptor("read", "1")], Policies(tool), sourceVersions: Sources());
        first.ShouldBe(equal);
        first.GetHashCode().ShouldBe(equal.GetHashCode());
        first.ShouldNotBe(Create([tool], Policies(tool), sourceVersions: Sources("source-8")));
        first.ShouldNotBe(Create([tool], Policies(tool), sourceVersions: Sources().Add(new("empty-source"), new("v1"))));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Create();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ImmutableDictionary<ToolSourceId, ToolSourceVersion> Sources(string version = "source-7") =>
        ImmutableDictionary<ToolSourceId, ToolSourceVersion>.Empty.Add(new ToolSourceId("agentkit.tools.tests"), new ToolSourceVersion(version));

    private static ToolCatalogSnapshot Create(
        ImmutableArray<ToolDescriptor>? tools = null,
        ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference>? executionPolicies = null,
        ImmutableDictionary<ToolAlias, ToolIdentity>? providerAliases = null,
        AgentId? agentId = null,
        SessionId? sessionId = null,
        RunId? runId = null,
        ExecutionIdentity? identity = null,
        SecurityPolicySnapshotReference? securityPolicy = null,
        ConfigurationVersion? configurationVersion = null,
        ToolCatalogVersion? version = null,
        ImmutableDictionary<ToolSourceId, ToolSourceVersion>? sourceVersions = null) => new(
            agentId ?? Agent(), sessionId ?? Session(), runId ?? Run(), identity ?? Identity(),
            securityPolicy ?? SecurityPolicy(), new AgentDefinitionRevision(0),
            configurationVersion ?? new ConfigurationVersion(4), version ?? new ToolCatalogVersion("catalog-7"),
            sourceVersions ?? Sources(), tools ?? [], executionPolicies ?? EmptyPolicies(),
            providerAliases ?? EmptyAliases());

    private static ToolCatalogSnapshot CreateInvalid(InvalidField field) => new(
            field is InvalidField.AgentId ? default : Agent(),
            field is InvalidField.SessionId ? default : Session(),
            field is InvalidField.RunId ? default : Run(),
            field is InvalidField.Identity ? null! : Identity(),
            field is InvalidField.SecurityPolicy ? null! : SecurityPolicy(), new AgentDefinitionRevision(0),
            field is InvalidField.ConfigurationVersion ? default : new ConfigurationVersion(4),
            field is InvalidField.Version ? default : new ToolCatalogVersion("catalog-7"),
            field is InvalidField.SourceVersions ? null! : Sources(),
            field is InvalidField.Tools ? default : [],
            field is InvalidField.ExecutionPolicies ? null! : EmptyPolicies(),
            field is InvalidField.ProviderAliases ? null! : EmptyAliases());

    private static ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference> Policies(params ToolDescriptor[] tools) =>
        tools.ToImmutableDictionary(Identity, static _ => Policy());

    private static ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference> EmptyPolicies() =>
        [];

    private static ImmutableDictionary<ToolAlias, ToolIdentity> EmptyAliases() =>
        [];

    private static ToolIdentity Identity(ToolDescriptor descriptor) => new(descriptor.Id, descriptor.Version);
    private static ToolExecutionPolicyReference Policy() => new(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1));
    private static AgentId Agent() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static SessionId Session() => new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static RunId Run() => new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(
        new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static SecurityPolicySnapshotReference SecurityPolicy() => new(
        new SecurityPolicySnapshotId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
        new SecurityPolicyVersion(1), new ContentHash("sha256:test"));

    private static ToolDescriptor Descriptor(string id, string version, string name = "tool")
    {
        using var document = JsonDocument.Parse("{}");
        return new ToolDescriptor(
            new ToolId(id), new ToolVersion(version), name, "description",
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement),
            null, new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId("agentkit.tools.tests"), ExtensionData.Empty);
    }

    private sealed class AliasIgnoreCaseComparer: IEqualityComparer<ToolAlias>
    {
        public bool Equals(ToolAlias x, ToolAlias y) => StringComparer.OrdinalIgnoreCase.Equals(x.Value, y.Value);
        public int GetHashCode(ToolAlias obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value);
    }

    private sealed class AlwaysEqualPolicyComparer: IEqualityComparer<ToolExecutionPolicyReference>
    {
        public bool Equals(ToolExecutionPolicyReference? x, ToolExecutionPolicyReference? y) => true;
        public int GetHashCode(ToolExecutionPolicyReference obj) => 0;
    }

    private sealed class NeverEqualAliasComparer: IEqualityComparer<ToolAlias>
    {
        public bool Equals(ToolAlias x, ToolAlias y) => false;
        public int GetHashCode(ToolAlias obj) => EqualityComparer<ToolAlias>.Default.GetHashCode(obj);
    }

    private sealed class NeverEqualIdentityComparer: IEqualityComparer<ToolIdentity>
    {
        public bool Equals(ToolIdentity x, ToolIdentity y) => false;
        public int GetHashCode(ToolIdentity obj) => EqualityComparer<ToolIdentity>.Default.GetHashCode(obj);
    }

    private enum InvalidField
    {
        AgentId,
        SessionId,
        RunId,
        Identity,
        SecurityPolicy,
        ConfigurationVersion,
        Version,
        SourceVersions,
        Tools,
        ExecutionPolicies,
        ProviderAliases,
    }
}
