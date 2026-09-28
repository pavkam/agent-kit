// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="AuthorizedDurableRequest{TRequest}"/> behavior and contracts.</summary>
public sealed class AuthorizedDurableRequestTests
{
    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedDurableRequest<DurableOperationAddress>(null!, JournalKey, Grant(), Intent()));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenJournalKeyIsDefault_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedDurableRequest<DurableOperationAddress>(
                DurabilityTestData.Address(),
                default,
                Grant(),
                Intent()));

        exception.ParamName.ShouldBe("journalKey");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedDurableRequest<DurableOperationAddress>(
                DurabilityTestData.Address(),
                JournalKey,
                null!,
                Intent()));

        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Constructor_WhenIntentIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedDurableRequest<DurableOperationAddress>(
                DurabilityTestData.Address(),
                JournalKey,
                Grant(),
                null!));

        exception.ParamName.ShouldBe("intent");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = DurabilityTestData.Address();
        var grant = Grant();
        var intent = Intent();

        var authorized = new AuthorizedDurableRequest<DurableOperationAddress>(request, JournalKey, grant, intent);

        authorized.Request.ShouldBe(request);
        authorized.JournalKey.ShouldBe(JournalKey);
        authorized.Grant.ShouldBe(grant);
        authorized.Intent.ShouldBe(intent);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var authorized = new AuthorizedDurableRequest<DurableOperationAddress>(
            DurabilityTestData.Address(),
            JournalKey,
            Grant(),
            Intent());

        (authorized with { }).ShouldBe(authorized);
    }

    private static DurableJournalKey JournalKey => new("journal");

    private static SecurityGrant Grant() => DurabilityTestData.Grant();

    private static SecurityEnforcementIntent Intent() => DurabilityTestData.Intent();
}
