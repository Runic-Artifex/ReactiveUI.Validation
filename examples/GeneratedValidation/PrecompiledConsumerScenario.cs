// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using GeneratedValidation.Precompiled;
#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Capabilities;
#else
using ReactiveUI.Validation.Capabilities;
#endif

internal static class PrecompiledConsumerScenario
{
    internal static void Run()
    {
        RunDelegates();
        var first = new PeerModel("first", "changed");
        var second = new PeerModel("second", "other");
        var firstCapture = new PeerCapture();
        var secondCapture = new PeerCapture();
        using var registry = new ValidationPlanRegistry(4);
        using var firstAttachment = registry.Attach(first);
        using var secondAttachment = registry.Attach(second);
        var registration = PeerRules.Register(registry);
        using var firstRule = PeerRules.Rule(first, firstCapture, "first", "first failure");
        using var secondRule = PeerRules.Rule(second, secondCapture, "second", "second failure");
        Require(firstRule.IsValid && secondRule.IsValid, "Fresh expressions must bind each current receiver.");
        firstCapture.Index = 1;
        Require(!firstRule.IsValid && secondRule.IsValid, "Live captures must invalidate only their own selection.");
        second.Set(0, "changed");
        Require(!secondRule.IsValid, "The second current receiver must remain independently observed.");

        var callable = PeerRules.Callable();
        var beforeUnknown = first.Reads;
        RequireMissing(() => callable(first, PeerRules.ForeignExpression(firstCapture), static _ => false, "unknown closure"),
            "An unknown compiler closure must not be discovered or executed.");
        Require(first.Reads == beforeUnknown, "Missing closure lookup must not read the selected root.");
        using var callableRule = callable(second, PeerRules.FreshExpression(secondCapture), static value => value == "changed", "callable failure");
        Require(callableRule.IsValid, "A normal API method group must execute the registered plan.");
        secondCapture.Index = 1;
        Require(!callableRule.IsValid, "Callable lookup must bind the invocation's live capture.");

        using (var conflicting = PeerRules.Register(registry, "peer.conflicting"))
        {
            RequireMissing(() => PeerRules.Rule(first, new PeerCapture(), "first", "ambiguous"), "Overlapping finite schemas must fail closed.",
                "Multiple validation schemas matched");
        }
        registration.Dispose();
        RequireMissing(() => PeerRules.Rule(first, new PeerCapture(), "first", "removed"), "Unregistered fresh expressions must fail loudly.");
        firstRule.Dispose();
        secondRule.Dispose();
        callableRule.Dispose();
        Require(first.ListenerCount == 0 && second.ListenerCount == 0 && firstCapture.ListenerCount == 0 && secondCapture.ListenerCount == 0,
            "Disposal must release every owned registration without disposing borrowed roots.");
        var reads = first.Reads + second.Reads;
        firstCapture.Index = 0;
        second.Set(1, "detached");
        Require(first.Reads + second.Reads == reads, "Detached roots must not be read by stale callbacks.");
        RequireMissing(() => callable(first, PeerRules.FreshExpression(firstCapture), static _ => false, "unregistered callable"),
            "The callable route must share the same unregistered control.");
    }

    private static void RunDelegates()
    {
        var first = new PeerModel("first", "changed");
        var second = new PeerModel("second", "other");
        var firstCapture = new PeerCapture();
        var secondCapture = new PeerCapture();
        var firstSelection = new PeerDelegateSelection(firstCapture);
        var secondSelection = new PeerDelegateSelection(secondCapture);
        using var registry = new ValidationPlanRegistry(4);
        using var firstAttachment = registry.Attach(first);
        using var secondAttachment = registry.Attach(second);
        using var firstRegistration = PeerRules.RegisterDelegate(registry, firstSelection, "peer.delegate.first");
        using var secondRegistration = PeerRules.RegisterDelegate(registry, secondSelection, "peer.delegate.second");
        using var firstRule = PeerRules.DelegateRule(first, firstSelection, "first", "delegate first failure");
        using var secondRule = PeerRules.DelegateRule(second, secondSelection, "second", "delegate second failure");
        Require(firstRule.IsValid && secondRule.IsValid, "Func callers must bind each invocation's current receiver.");
        firstCapture.Index = 1;
        Require(!firstRule.IsValid && secondRule.IsValid, "Known Func captures must retain independent live paths.");
        second.Set(0, "changed");
        Require(!secondRule.IsValid, "Func selection must observe the current second receiver.");

        var callable = PeerRules.DelegateCallable();
        var storedSelector = secondSelection.Fresh();
        var equalFreshSelector = secondSelection.Fresh();
        Require(!ReferenceEquals(storedSelector, equalFreshSelector) && storedSelector.Equals(equalFreshSelector),
            "The callable control must supply genuinely fresh equal method-group delegates.");
        using var callableRule = callable(second, storedSelector, static value => value == "changed", "Func callable failure");
        Require(callableRule.IsValid, "Fresh equal method groups must execute the supplied cold plan.");
        secondCapture.Index = 1;
        Require(!callableRule.IsValid, "Func callable dispatch must bind the current live capture.");
        var unknown = new PeerDelegateSelection(firstCapture);
        var beforeMissing = first.Reads + second.Reads;
        RequireMissing(() => PeerRules.DelegateRule(first, unknown, "first", "unknown delegate"),
            "A different delegate owner must fail closed even with the same typed capture.");
        RequireMissing(() => callable(first, unknown.Fresh(), static _ => false, "unknown callable"),
            "An unknown Func callable selector must fail closed.");
        Require(first.Reads + second.Reads == beforeMissing && unknown.MetadataReads == 0,
            "Missing Func lookup must not execute supplied selectors or selected getters.");
        using (var conflicting = PeerRules.RegisterDelegate(registry, firstSelection, "peer.delegate.conflicting"))
        {
            RequireMissing(() => PeerRules.DelegateRule(first, firstSelection, "changed", "ambiguous delegate"),
                "Overlapping known Func schemas must fail closed.", "Multiple validation schemas matched");
            Require(first.Reads + second.Reads == beforeMissing, "Ambiguous Func lookup must acquire no selected reads.");
        }

        firstRegistration.Dispose();
        RequireMissing(() => PeerRules.DelegateRule(first, firstSelection, "changed", "removed delegate"),
            "Removed Func registrations must reject later normal invocations.");
        RequireMissing(() => callable(first, firstSelection.Fresh(), static _ => false, "removed Func callable"),
            "Removed Func registrations must reject later callable invocations.");
        secondAttachment.Dispose();
        RequireMissing(() => callable(second, equalFreshSelector, static _ => false, "detached Func scope"),
            "A detached registration scope must reject new callable invocations.");
        registry.Dispose();
        first.Set(1, "first");
        Require(firstRule.IsValid, "Registration disposal must leave existing helpers and borrowed roots usable.");
        firstRule.Dispose();
        secondRule.Dispose();
        callableRule.Dispose();
        Require(first.ListenerCount == 0 && second.ListenerCount == 0 && firstCapture.ListenerCount == 0 && secondCapture.ListenerCount == 0,
            "Func helper disposal must release every source and capture subscription.");
        var reads = first.Reads + second.Reads;
        firstCapture.Index = 0;
        second.Set(1, "detached");
        Require(first.Reads + second.Reads == reads, "Disposed Func helpers must not read detached roots.");
        Require(firstSelection.MetadataReads == 0 && secondSelection.MetadataReads == 0 && unknown.MetadataReads == 0,
            "Registered Func routes must never execute metadata delegates.");
    }

    private static void RequireMissing(Func<ValidationHelper> action, string failure, string expected = "No typed validation capability is registered for RuleValue")
    {
        try
        {
            using var unexpected = action();
        }
        catch (InvalidOperationException error) when (error.Message.Contains(expected, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(failure);
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }
}
