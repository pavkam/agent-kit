// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerStateException behavior and contracts.</summary>
public sealed class BudgetLedgerStateExceptionTests
{
    [Fact]
    public void BudgetLedgerStateException_WhenSafeMessageIsInvalid_ThrowsArgumentException() => AssertSafeMessageValidation(message => new BudgetLedgerStateException(message));
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

    [Fact]
    public void BudgetLedgerSafeExceptions_WhenSafeMessageIsValid_PreserveSafeMessage()
    {
        const string message = "safe";
        new BudgetLedgerStateException(message).SafeMessage.ShouldBe(message);
    }
}
