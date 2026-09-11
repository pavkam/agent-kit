// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerPersistenceUnavailableException behavior and contracts.</summary>
public sealed class BudgetLedgerPersistenceUnavailableExceptionTests
{
    [Fact]
    public void BudgetLedgerPersistenceUnavailableException_WhenSafeMessageIsInvalid_ThrowsArgumentException() => AssertSafeMessageValidation(message => new BudgetLedgerPersistenceUnavailableException(message, false));
    [Fact]
    public void BudgetLedgerPersistenceUnavailableException_WhenAcknowledgementIsKnownOrUnknown_PreservesState()
    {
        new BudgetLedgerPersistenceUnavailableException("safe", false).AcknowledgementUnknown.ShouldBeFalse();
        new BudgetLedgerPersistenceUnavailableException("safe", true).AcknowledgementUnknown.ShouldBeTrue();
    }

    [Fact]
    public void BudgetLedgerPersistenceUnavailableException_WhenInnerExceptionIsSupplied_PreservesCausalFailure()
    {
        var cause = new InvalidOperationException("adapter");
        var exception = new BudgetLedgerPersistenceUnavailableException("safe", true, cause);
        exception.SafeMessage.ShouldBe("safe");
        exception.AcknowledgementUnknown.ShouldBeTrue();
        exception.InnerException.ShouldBeSameAs(cause);
    }

    [Fact]
    public void BudgetLedgerPersistenceUnavailableException_WhenInnerExceptionIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetLedgerPersistenceUnavailableException("safe", false, null!));
        exception.ParamName.ShouldBe("innerException");
    }

    private static void AssertSafeMessageValidation<TException>(Func<string, TException> create)
        where TException : InvalidOperationException
    {
        var nullMessage = Should.Throw<ArgumentNullException>(() => create(null!));
        var emptyMessage = Should.Throw<ArgumentException>(() => create(string.Empty));
        var whitespaceMessage = Should.Throw<ArgumentException>(() => create(" "));
        nullMessage.ParamName.ShouldBe("safeMessage");
        emptyMessage.ParamName.ShouldBe("safeMessage");
        whitespaceMessage.ParamName.ShouldBe("safeMessage");
    }
}
