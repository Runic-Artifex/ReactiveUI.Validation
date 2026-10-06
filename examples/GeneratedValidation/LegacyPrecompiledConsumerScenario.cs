// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using GeneratedValidation.LegacyPeer;
using System.Linq.Expressions;
#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Capabilities;
#else
using ReactiveUI.Validation.Capabilities;
#endif

internal static class LegacyPrecompiledConsumerScenario
{
    internal static void Run()
    {
        var first = new PeerModel("first", "changed");
        var second = new PeerModel("second", "other");
        var firstCapture = new PeerCapture();
        var secondCapture = new PeerCapture();
        using var registry = new ValidationPlanRegistry(4);
        using var firstAttachment = registry.Attach(first);
        using var secondAttachment = registry.Attach(second);
        var registration = Register(registry);
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

        using (var conflicting = Register(registry, "peer.conflicting"))
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

    /// <summary>Registers one finite capture schema; lookup binds each current source and capture.</summary>
    private static IDisposable Register(ValidationPlanRegistry registry, string operationIdentity = "peer.read.capture")
    {
        var exemplar = PeerRules.FreshExpression(new PeerCapture());
        var capture = new ValidationArgument<PeerCapture>();
        var position = (ConstantExpression)((MethodCallExpression)exemplar.Body).Arguments[0];
        var matcher = ValidationExpressionPattern.Create(exemplar, ValidationExpressionBinding.Constant(position, capture));
        return registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, operationIdentity, matcher,
            arguments => Selector(arguments.Get(capture)));
    }

    private static ValidationSelector<PeerModel, string?> Selector(PeerCapture capture) => new(source => new(
        () => ValidationRead<string?>.Present(source.Read(capture), Paths(source, capture)),
        [ValidationDependency.PropertyChanged(() => source, "Item[]"), ValidationDependency.PropertyChanged(() => capture, nameof(PeerCapture.Index))],
        new(ValidationMissingOwnerPolicy.DefaultValue, null, EqualityComparer<string?>.Default, true),
        () => Paths(source, capture)));

    private static IReadOnlyList<ValidationPath> Paths(PeerModel source, PeerCapture capture) =>
        [ValidationPath.Structural($"Values[{capture.Index}]", (source, capture.Index), EqualityComparer<(PeerModel, int)>.Default)];

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
