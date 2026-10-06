// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
using System.Reactive.Linq;
#else
using Observable = ReactiveUI.Primitives.Signals.Signal;
#endif

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Verifies compatibility seeds at ownership handoff independently of actual domain states.</summary>
public class CapabilityLegacySequenceTests
{
    /// <summary>The first matching state's text.</summary>
    private const string FirstText = "first";

    /// <summary>The second matching state's text.</summary>
    private const string SecondText = "second";

    /// <summary>The newly admitted state's text.</summary>
    private const string ThirdText = "third";

    /// <summary>The different property state's text.</summary>
    private const string PropertyText = "property";

    /// <summary>The replacement context state's text.</summary>
    private const string ContextText = "context";

    /// <summary>The explicit trace of one empty collection.</summary>
    private const string EmptyTrace = "empty";

    /// <summary>The explicit trace of the singleton compatibility valid state.</summary>
    private const string ValidTrace = "valid";

    /// <summary>The original two matching complete states.</summary>
    private const string OriginalText = $"{FirstText}|{SecondText}";

    /// <summary>The complete membership after the third rule is admitted.</summary>
    private const string FullText = $"{FirstText}|{SecondText}|{ThirdText}";

    /// <summary>The exact absent-owner presentation trace.</summary>
    private static readonly string[] EmptyOnly = [EmptyTrace];

    /// <summary>Initial, context and path switches have empty preludes; membership alone only has a valid seed.</summary>
    /// <param name="initial">The explicitly selected presentation policy.</param>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    [Arguments(ValidationInitialSequence.Actual)]
    [Arguments(ValidationInitialSequence.LegacyEmpty)]
    [Arguments(ValidationInitialSequence.LegacyValid)]
    [Arguments(ValidationInitialSequence.LegacyEmpty | ValidationInitialSequence.LegacyValid)]
    public async Task PropertySequenceSeparatesHandoffAndMembershipSeeds(ValidationInitialSequence initial)
    {
        using var model = new TestViewModel();
        using var replacement = new TestViewModel();
        var view = new TestView(model);
        var firstState = new ValidationState(false, FirstText);
        var secondState = new ValidationState(false, SecondText);
        using var firstStates = new BehaviorSubject<IValidationState>(firstState);
        using var secondStates = new BehaviorSubject<IValidationState>(secondState);
        using var first = model.AddObservableRule(firstStates, [nameof(TestViewModel.Name)]);
        using var second = model.AddObservableRule(secondStates, [nameof(TestViewModel.Name)]);
        using var other = model.AddObservableRule(Observable.Return<IValidationState>(new ValidationState(false, PropertyText)), [nameof(TestViewModel.Name2)]);
        using var replacementRule = replacement.AddObservableRule(Observable.Return<IValidationState>(new ValidationState(false, ContextText)), [nameof(TestViewModel.Name2)]);
        var selectedContext = new ValidationCell<IValidationContext?>(model.ValidationContext);
        var selectedPath = new ValidationCell<ValidationPath>(ValidationPath.Legacy(nameof(TestViewModel.Name)));
        var contexts = new ValidationSelector<TestViewModel, IValidationContext?>(_ => new(
            () => ValidationRead<IValidationContext?>.Present(selectedContext.Value, []),
            [ValidationDependency.PropertyChanged(() => selectedContext, nameof(selectedContext.Value))],
            ValidationObservationOptions<IValidationContext?>.Default));
        var paths = new ValidationSelector<TestViewModel, ValidationPath>(_ => new(
            () => ValidationRead<ValidationPath>.Present(selectedPath.Value, []),
            [ValidationDependency.PropertyChanged(() => selectedPath, nameof(selectedPath.Value))],
            ValidationObservationOptions<ValidationPath>.Default));
        var raw = new List<string>();
        var text = new List<string>();
        var domain = new List<bool>();
        IList<IValidationState>? actualStates = null;
        using var binding = ValidationRuntime.Bind(
            ValidationRuntime.ObserveViewProperty(view, contexts, paths, true, initial),
            states =>
            {
                actualStates = states;
                raw.Add(Trace(states));
                text.Add(string.Join("|", ValidationRuntime.FormatAll(states, SingleLineFormatter.Default)));
                domain.Add(model.ValidationContext.GetIsValid());
            });
        await Assert.That(raw.SequenceEqual(Handoff(initial, OriginalText))).IsTrue();
        await Assert.That(text.SequenceEqual(HandoffText(initial, OriginalText))).IsTrue();
        await Assert.That(domain.TrueForAll(static valid => !valid)).IsTrue();
        await Assert.That(actualStates![0]).IsSameReferenceAs(firstState);
        await Assert.That(actualStates[1]).IsSameReferenceAs(secondState);
        raw.Clear();
        text.Clear();
        using var third = model.AddObservableRule(Observable.Return<IValidationState>(new ValidationState(false, ThirdText)), [nameof(TestViewModel.Name)]);
        var membership = Handoff(initial & ValidationInitialSequence.LegacyValid, FullText);
        await Assert.That(raw.SequenceEqual(membership)).IsTrue();
        await VerifyOwnershipSwitches(view, selectedContext, selectedPath, replacement.ValidationContext, model, raw, initial);
    }

    /// <summary>Verifies path, context and model replacement and each null-owner boundary.</summary>
    /// <param name="view">The notifying view.</param>
    /// <param name="selectedContext">The explicit context selection storage.</param>
    /// <param name="selectedPath">The explicit path selection storage.</param>
    /// <param name="replacementContext">The borrowed replacement context.</param>
    /// <param name="model">The borrowed model selected again after absence.</param>
    /// <param name="raw">The ordered callback trace.</param>
    /// <param name="initial">The presentation policy.</param>
    /// <returns>The asynchronous assertion task.</returns>
    private static async Task VerifyOwnershipSwitches(
        TestView view,
        ValidationCell<IValidationContext?> selectedContext,
        ValidationCell<ValidationPath> selectedPath,
        IValidationContext replacementContext,
        TestViewModel model,
        List<string> raw,
        ValidationInitialSequence initial)
    {
        raw.Clear();
        selectedPath.Value = ValidationPath.Legacy(nameof(TestViewModel.Name2));
        await Assert.That(raw.SequenceEqual(Handoff(initial, PropertyText))).IsTrue();
        raw.Clear();
        selectedContext.Value = replacementContext;
        await Assert.That(raw.SequenceEqual(Handoff(initial, ContextText))).IsTrue();
        raw.Clear();
        selectedContext.Value = null;
        await Assert.That(raw.SequenceEqual(EmptyOnly)).IsTrue();
        raw.Clear();
        selectedContext.Value = replacementContext;
        await Assert.That(raw.SequenceEqual(Handoff(initial, ContextText))).IsTrue();
        raw.Clear();
        view.ViewModel = null;
        await Assert.That(raw.SequenceEqual(EmptyOnly)).IsTrue();
        raw.Clear();
        view.ViewModel = model;
        await Assert.That(raw.SequenceEqual(Handoff(initial, ContextText))).IsTrue();
    }

    /// <summary>Builds the ordered raw trace for one context/path ownership selection.</summary>
    /// <param name="initial">The presentation policy.</param>
    /// <param name="actual">The actual matching state trace.</param>
    /// <returns>The complete ordered handoff trace.</returns>
    private static List<string> Handoff(ValidationInitialSequence initial, string actual)
    {
        List<string> expected = [];
        if ((initial & ValidationInitialSequence.LegacyEmpty) != 0)
        {
            expected.Add(EmptyTrace);
        }

        if ((initial & ValidationInitialSequence.LegacyValid) != 0)
        {
            expected.Add(ValidTrace);
        }

        expected.Add(actual);
        return expected;
    }

    /// <summary>Builds the formatter trace without promoting presentation seeds into domain rules.</summary>
    /// <param name="initial">The presentation policy.</param>
    /// <param name="actual">The formatted matching states.</param>
    /// <returns>The formatted ordered handoff trace.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IEnumerable<string> HandoffText(ValidationInitialSequence initial, string actual) =>
        Handoff(initial, actual).Select(static value => value is EmptyTrace or ValidTrace ? string.Empty : value);

    /// <summary>Identifies complete raw states and compatibility seeds distinctly.</summary>
    /// <param name="states">The current matching complete states.</param>
    /// <returns>The exact trace.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Trace(IList<IValidationState> states) => states.Count switch
    {
        0 => EmptyTrace,
        1 when ReferenceEquals(states[0], ValidationState.Valid) => ValidTrace,
        _ => string.Join("|", states.Select(static state => state.Text.ToSingleLine())),
    };
}
