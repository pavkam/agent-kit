// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Verifies PortableSessionSecurityJson behavior and contracts.</summary>
public sealed class PortableSessionSecurityJsonTests
{
    private static readonly SessionEntryCodecLimits Limits = new(1_048_576, 64, 65_536, 16);

    [Theory]
    [InlineData(ExecutionSubjectKind.Service)]
    [InlineData(ExecutionSubjectKind.Workload)]
    [InlineData(ExecutionSubjectKind.Anonymous)]
    public void WriteAndTryIdentity_WhenSubjectKindVaries_RoundTrips(ExecutionSubjectKind subjectKind)
    {
        var identity = Identity(subjectKind: subjectKind);

        var element = Serialize(identity);
        var count = 0;
        var bytes = 0;

        var ok = PortableSessionSecurityJson.TryIdentity(element, Limits, ref count, ref bytes, out var decoded);

        ok.ShouldBeTrue();
        decoded.SubjectKind.ShouldBe(subjectKind);
    }

    [Theory]
    [InlineData(IdentityClaimValueKind.Boolean)]
    [InlineData(IdentityClaimValueKind.WholeNumber)]
    public void WriteAndTryIdentity_WhenClaimValueKindVaries_RoundTrips(IdentityClaimValueKind kind)
    {
        var identity = Identity(claimKind: kind);

        var element = Serialize(identity);
        var count = 0;
        var bytes = 0;

        var ok = PortableSessionSecurityJson.TryIdentity(element, Limits, ref count, ref bytes, out var decoded);

        ok.ShouldBeTrue();
        decoded.Claims[0].ValueKind.ShouldBe(kind);
    }

    [Theory]
    [InlineData(IdentityAssuranceLevel.Anonymous)]
    [InlineData(IdentityAssuranceLevel.Strong)]
    [InlineData(IdentityAssuranceLevel.HardwareBacked)]
    public void WriteAndTryIdentity_WhenAssuranceVaries_RoundTrips(IdentityAssuranceLevel assurance)
    {
        var identity = Identity(assurance: assurance, delegationAssurance: assurance);

        var element = Serialize(identity);
        var count = 0;
        var bytes = 0;

        var ok = PortableSessionSecurityJson.TryIdentity(element, Limits, ref count, ref bytes, out var decoded);

        ok.ShouldBeTrue();
        decoded.Assurance.ShouldBe(assurance);
        decoded.DelegationChain[0].Assurance.ShouldBe(assurance);
    }

    [Fact]
    public void WriteIdentity_WhenEvidenceHasNoExpiry_WritesNull()
    {
        var identity = Identity(hasExpiry: false);

        var element = Serialize(identity);

        element.GetProperty("identity").GetProperty("evidence").GetProperty("expiresAt").ValueKind
            .ShouldBe(JsonValueKind.Null);
        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(element, Limits, ref count, ref bytes, out var decoded);
        ok.ShouldBeTrue();
        decoded.Evidence.ExpiresAt.ShouldBeNull();
    }

    [Fact]
    public void TryIdentity_WhenSubjectKindTextIsUnrecognized_ReturnsFalse()
    {
        var element = Serialize(Identity());
        var node = ToNode(element);
        node["identity"]!["subjectKind"] = "unrecognized";

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(Reparse(node), Limits, ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryIdentity_WhenClaimValueKindTextIsUnrecognized_ReturnsFalse()
    {
        var element = Serialize(Identity());
        var node = ToNode(element);
        node["identity"]!["claims"]![0]!["valueKind"] = "unrecognized";

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(Reparse(node), Limits, ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryIdentity_WhenAssuranceTextIsUnrecognized_ReturnsFalse()
    {
        var element = Serialize(Identity());
        var node = ToNode(element);
        node["identity"]!["assurance"] = "unrecognized";

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(Reparse(node), Limits, ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryIdentity_WhenIdentityObjectIsMissing_ReturnsFalse()
    {
        var count = 0;
        var bytes = 0;

        var ok = PortableSessionSecurityJson.TryIdentity(JsonDocument.Parse("{}").RootElement, Limits,
            ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryIdentity_WhenEvidenceIsMalformed_ReturnsFalse()
    {
        var element = Serialize(Identity());
        var node = ToNode(element);
        _ = node["identity"]!["evidence"]!.AsObject().Remove("method");

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(Reparse(node), Limits, ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryIdentity_WhenClaimsFieldIsMissing_ReturnsFalse()
    {
        var element = Serialize(Identity());
        var node = ToNode(element);
        _ = node["identity"]!.AsObject().Remove("claims");

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(Reparse(node), Limits, ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryIdentity_WhenClaimsFieldIsNotAnArray_ReturnsFalse()
    {
        var element = Serialize(Identity());
        var node = ToNode(element);
        node["identity"]!["claims"] = 1;

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(Reparse(node), Limits, ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryIdentity_WhenClaimEntryIsMalformed_ReturnsFalse()
    {
        var element = Serialize(Identity());
        var node = ToNode(element);
        _ = node["identity"]!["claims"]![0]!.AsObject().Remove("issuer");

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(Reparse(node), Limits, ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryIdentity_WhenDelegationChainFieldIsMissing_ReturnsFalse()
    {
        var element = Serialize(Identity());
        var node = ToNode(element);
        _ = node["identity"]!.AsObject().Remove("delegationChain");

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(Reparse(node), Limits, ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryIdentity_WhenDelegationChainFieldIsNotAnArray_ReturnsFalse()
    {
        var element = Serialize(Identity());
        var node = ToNode(element);
        node["identity"]!["delegationChain"] = 1;

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(Reparse(node), Limits, ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryIdentity_WhenDelegationEntryIsMalformed_ReturnsFalse()
    {
        var element = Serialize(Identity());
        var node = ToNode(element);
        _ = node["identity"]!["delegationChain"]![0]!.AsObject().Remove("issuer");

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryIdentity(Reparse(node), Limits, ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryAuthorization_WhenPolicySnapshotIsMalformed_ReturnsFalse()
    {
        var identity = Identity();
        var authorization = Authorization(identity);
        var element = Serialize(identity, authorization);
        var node = ToNode(element);
        _ = node["authorization"]!["policySnapshot"]!.AsObject().Remove("fingerprint");

        var count = 0;
        var bytes = 0;
        var ok = PortableSessionSecurityJson.TryAuthorization(Reparse(node), identity, Address, Correlation, Limits,
            ref count, ref bytes, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void ValidForEncode_WhenTenantIdContainsUnpairedSurrogate_ReturnsFalse()
    {
        var identity = Identity(tenantId: "tenant\uD800");
        var authorization = Authorization(identity);

        PortableSessionSecurityJson.ValidForEncode(identity, authorization, Limits).ShouldBeFalse();
    }

    [Fact]
    public void ValidForEncode_WhenClaimValueContainsUnpairedSurrogate_ReturnsFalse()
    {
        var identity = Identity(claimValue: "operator\uD800");
        var authorization = Authorization(identity);

        PortableSessionSecurityJson.ValidForEncode(identity, authorization, Limits).ShouldBeFalse();
    }

    [Fact]
    public void ValidForEncode_WhenDelegationPrincipalContainsUnpairedSurrogate_ReturnsFalse()
    {
        var identity = Identity(delegationPrincipal: "parent\uD800");
        var authorization = Authorization(identity);

        PortableSessionSecurityJson.ValidForEncode(identity, authorization, Limits).ShouldBeFalse();
    }

    [Fact]
    public void ValidForEncode_WhenDelegationClaimValueContainsUnpairedSurrogate_ReturnsFalse()
    {
        var identity = Identity(delegationClaimValue: "value\uD800");
        var authorization = Authorization(identity);

        PortableSessionSecurityJson.ValidForEncode(identity, authorization, Limits).ShouldBeFalse();
    }

    [Fact]
    public void ValidForEncode_WhenValid_ReturnsTrue()
    {
        var identity = Identity();
        var authorization = Authorization(identity);

        PortableSessionSecurityJson.ValidForEncode(identity, authorization, Limits).ShouldBeTrue();
    }

    private static readonly SessionAddress Address = new(new AgentId(Id(2)), new SessionId(Id(3)));
    private static readonly InRunOperationCorrelation Correlation =
        new(new OperationId(Id(4)), new RunId(Id(7)), new TurnId(Id(8)));

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static ExecutionIdentity Identity(
        ExecutionSubjectKind subjectKind = ExecutionSubjectKind.Human,
        IdentityClaimValueKind claimKind = IdentityClaimValueKind.Text,
        IdentityAssuranceLevel assurance = IdentityAssuranceLevel.Basic,
        IdentityAssuranceLevel delegationAssurance = IdentityAssuranceLevel.Basic,
        bool hasExpiry = true,
        string tenantId = "tenant",
        string claimValue = "operator",
        string delegationPrincipal = "parent",
        string delegationClaimValue = "delegate-value")
    {
        var claim = new IdentityClaim(new IdentityIssuerId("issuer"), "role", claimValue, claimKind);
        var authenticatedAt = new DateTimeOffset(1970, 1, 1, 2, 0, 0, TimeSpan.FromHours(2));
        var evidence = new AuthenticationEvidence(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId("issuer"),
            "mfa", authenticatedAt, hasExpiry ? authenticatedAt.AddHours(1) : null,
            new AuthenticationEvidenceFingerprint(new ContentHash("sha256:evidence")));
        var delegationClaim = new IdentityClaim(new IdentityIssuerId("issuer"), "role", delegationClaimValue, claimKind);
        var link = new DelegationIdentityLink(new DelegationId(Id(50)), new TenantId(tenantId),
            new PrincipalId(delegationPrincipal), new IdentityIssuerId("issuer"), new AuthenticationEvidenceId("parent-evidence"),
            new IdentityVersion(1), authenticatedAt, [delegationClaim], delegationAssurance);
        return new ExecutionIdentity(new TenantId(tenantId), new PrincipalId("principal"), subjectKind,
            evidence, [claim], [link], assurance, new IdentityVersion(1));
    }

    private static SecurityAuthorizationContext Authorization(ExecutionIdentity identity) => new(
        new SecurityProfileKey("secure"), new SecurityProfileVersion(2),
        new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Id(40)), new SecurityPolicyVersion(5),
            new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("primary"), new AgentDefinitionRevision(6), new ConfigurationVersion(3),
        new SecurityAuthorizationScope(Address.AgentId, Address.SessionId, Correlation), identity);

    private static JsonElement Serialize(ExecutionIdentity identity, SecurityAuthorizationContext? authorization = null)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            PortableSessionSecurityJson.WriteIdentity(writer, identity);
            if (authorization is not null)
            {
                PortableSessionSecurityJson.WriteAuthorization(writer, authorization);
            }
            writer.WriteEndObject();
        }
        return JsonDocument.Parse(buffer.WrittenMemory).RootElement;
    }

    private static JsonObject ToNode(JsonElement element) => JsonNode.Parse(element.GetRawText())!.AsObject();

    private static JsonElement Reparse(JsonObject node) => JsonDocument.Parse(node.ToJsonString()).RootElement;
}
