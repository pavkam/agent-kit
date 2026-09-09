// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Isolates runtime diagnostic observers from authoritative budget behavior.</summary>
internal sealed class BudgetRuntimeObservation: IDisposable
{
    private readonly AgentKitActivityScope _scope;

    private BudgetRuntimeObservation(AgentKitActivityScope scope)
    {
        Debug.Assert(scope is not null, "Start supplies a non-null safe activity scope.");
        _scope = scope;
    }

    /// <summary>Starts an optional shared activity without allowing a listener failure to affect budget behavior.</summary>
    /// <param name="name">The bounded shared activity name.</param>
    /// <returns>An observation whose disposal is safe when diagnostics are disabled or faulty.</returns>
    internal static BudgetRuntimeObservation Start(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(AgentKitActivityScope.Start(name, ActivityKind.Internal));
    }

    /// <summary>Attaches safe correlation to the activity when one exists.</summary>
    /// <param name="name">The shared tag name.</param>
    /// <param name="value">The non-content correlation value.</param>
    internal void Tag(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        try
        {
            _ = _scope.Activity?.SetTag(name, value);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Marks a terminal success without changing the underlying operation if an observer fails.</summary>
    /// <param name="outcome">The bounded semantic outcome.</param>
    internal void Success(string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        try
        {
            _scope.Activity.SetSuccessful(outcome);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Marks a terminal failure without changing the underlying operation if an observer fails.</summary>
    /// <param name="outcome">The bounded semantic outcome.</param>
    /// <param name="errorType">The normalized failure type.</param>
    internal void Failure(string outcome, string errorType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorType);
        try
        {
            _scope.Activity.SetFailed(outcome, errorType);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Marks an ordinary typed refusal without fabricating exception metadata.</summary>
    /// <param name="outcome">The bounded semantic refusal outcome.</param>
    internal void Rejected(string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        try
        {
            _ = _scope.Activity?.SetTag(AgentKitTagNames.Outcome, outcome);
            _ = (_scope.Activity?.SetStatus(ActivityStatusCode.Error));
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Stops the optional activity while isolating listener disposal failures.</summary>
    public void Dispose()
    {
        try
        {
            _scope.Dispose();
        }
        catch (Exception)
        {
        }
    }
}
