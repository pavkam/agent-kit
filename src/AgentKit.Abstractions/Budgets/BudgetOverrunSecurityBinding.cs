// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Builds and checks the canonical application-state resource for audited overrun resolution.</summary>
public static class BudgetOverrunSecurityBinding
{
    /// <summary>Fingerprints the complete exact target reference for operator-resolution enforcement.</summary>
    /// <param name="hold">The non-null hold generation, including both complete addresses and its accounting revision.</param>
    /// <returns>An algorithm-qualified canonical digest with unambiguous type and property encoding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hold"/> is null.</exception>
    public static InputFingerprint Fingerprint(BudgetOverrunHoldReference hold)
    {
        ArgumentNullException.ThrowIfNull(hold);
        return SecurityCanonicalFingerprint.Create(hold);
    }

    /// <summary>Creates the exact protected resource.</summary><param name="hold">The non-null hold generation.</param><returns>An application-state resource bound to its tenant, boundary, dimension-owning reservation, and accounting generation.</returns><exception cref="ArgumentNullException"><paramref name="hold"/> is null.</exception>
    public static ProtectedResource Resource(BudgetOverrunHoldReference hold)
    {
        ArgumentNullException.ThrowIfNull(hold);
        return new(ProtectedResourceKind.ApplicationState, $"budget-overrun:{Fingerprint(hold).Value}");
    }

    /// <summary>Checks structural audit binding without claiming receipt authenticity or authorization.</summary><param name="hold">The expected hold.</param><param name="receipt">The supplied audit receipt.</param><returns>True only when operation, effect, address, and sole resource match.</returns><exception cref="ArgumentNullException">An argument is null.</exception>
    public static bool Matches(BudgetOverrunHoldReference hold, SecurityEnforcementIntentReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(hold); ArgumentNullException.ThrowIfNull(receipt);
        var enforcement = receipt.Enforcement;
        return enforcement.Kind == SecurityOperationKind.StateMutation
            && enforcement.Effect == SecurityEffect.Mutate
            && enforcement.InputFingerprint == Fingerprint(hold)
            && enforcement.Resources.Length == 1
            && enforcement.Resources[0] == Resource(hold);
    }
}
