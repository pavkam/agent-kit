// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Resolves hook profile requests against every profile registered in <see cref="HookProfileRegistry"/>.</summary>
public sealed class DefaultHookProfileSelector: IHookProfileSelector
{
    private readonly HookProfileRegistry _profiles;

    /// <summary>Creates a selector that resolves only the built-in default profile.</summary>
    /// <returns>A selector suitable for tests and isolated loop fixtures.</returns>
    public static DefaultHookProfileSelector CreateWithDefaultProfileOnly() => new(new HookProfileRegistry());

    /// <summary>Initializes a selector backed by the composition's profile registry.</summary>
    /// <param name="profiles">The registry populated during service-provider construction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profiles"/> is null.</exception>
    internal DefaultHookProfileSelector(HookProfileRegistry profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        _profiles = profiles;
    }

    /// <inheritdoc/>
    public ValueTask<HookProfileSelectionResult> SelectAsync(
        HookProfileSelectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var key = request.RequestedProfile ?? HookProfileOptions.DefaultProfileKey;
        return _profiles.Contains(key)
            ? new ValueTask<HookProfileSelectionResult>(new HookProfileSelected(key))
            : new ValueTask<HookProfileSelectionResult>(new HookProfileUnavailable(key));
    }
}
