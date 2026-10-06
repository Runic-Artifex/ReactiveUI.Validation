// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using System.Reactive.Subjects;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Exercises explicit typed dispatch through unchanged normal CLR entry points.</summary>
[ValidationRuntimeDispatch]
public class CapabilityFacadeTests
{
    /// <summary>The explicit formatter output.</summary>
    private const string SelectedFormat = "selected";

    /// <summary>The invalid validation message.</summary>
    private const string ErrorMessage = "error";

    /// <summary>The small registry registration and owner capacity.</summary>
    private const int SmallRegistryCapacity = 2;

    /// <summary>The capacity for the complete static factory fixture.</summary>
    private const int FactoryRegistryCapacity = 8;

    /// <summary>The capacity for the predicate rule fixture.</summary>
    private const int RuleRegistryCapacity = 4;

    /// <summary>The rich metadata used by all static factory routes.</summary>
    private const int FactoryStateRevision = 17;

    /// <summary>The metadata used by the leaf-read boundary fixture.</summary>
    private const int LeafGetterStateRevision = 5;

    /// <summary>The rich metadata supplied by the newly selected model.</summary>
    private const int SelectedModelStateRevision = 9;

    /// <summary>The first selected model state metadata.</summary>
    private const int FirstModelStateRevision = 3;

    /// <summary>The replacement model state metadata.</summary>
    private const int SecondModelStateRevision = 7;

    /// <summary>The metadata carried across structural path changes.</summary>
    private const int MetadataStateRevision = 23;

    /// <summary>The number of selected models whose factories execute.</summary>
    private const int SelectedModelCount = 2;

    /// <summary>The number of rules retained by the advisory context.</summary>
    private const int AdvisoryRuleCount = 2;

    /// <summary>The second structural property identity.</summary>
    private const int SecondPropertyKey = 2;

    /// <summary>All eleven static factories resolve registered capabilities and retain complete states.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task AllStaticFactoriesExecuteRegisteredPlans()
    {
        using var model = new TestViewModel();
        var view = new TestView(model);
        var rich = new RichState(FactoryStateRevision);
        using var states = new BehaviorSubject<IValidationState>(rich);
        using var rule = model.AddObservableRule(states, [nameof(TestViewModel.Name)]);
        model.NameRule = rule;
        Expression<Func<TestViewModel, string?>> property = vm => vm.Name;
        Expression<Func<TestViewModel?, ValidationHelper?>> helper = vm => vm!.NameRule;
        Expression<Func<TestView, string>> target = v => v.NameErrorLabel;
        using var registry = new ValidationPlanRegistry(FactoryRegistryCapacity);
        using var attachment = registry.Attach(view);
        using var propertyLease = registry.RegisterSelector(ValidationPlanRole.Property, property, NameMetadata());
        using var helperLease = registry.RegisterSelector(
            ValidationPlanRole.Helper,
            helper,
            new(static source => HelperSelector().Bind(source!)));
        using var targetLease = registry.RegisterTarget(ValidationPlanRole.Target, target, TextTarget());
        var formatter = new ConstFormatter(SelectedFormat);
        IValidationState? propertyState = null;
        IValidationState? helperState = null;
        IList<string>? formatted = null;
        string? aggregate = null;
        using var first = ValidationBinding.ForProperty(view, property, target);
        using var second = ValidationBinding.ForProperty(view, property, target, formatter);
        using var third = ValidationBinding.ForProperty(view, property, target, formatter, true);
        using var fourth = ValidationBinding.ForProperty(
            view,
            property,
            (current, output) =>
            {
                propertyState = current[0];
                formatted = output;
            },
            formatter);
        using var fifth = ValidationBinding.ForProperty(view, property, (current, _) => propertyState = current[0], formatter, true);
        using var sixth = ValidationBinding.ForValidationHelperProperty(view, helper, target);
        using var seventh = ValidationBinding.ForValidationHelperProperty(view, helper, target, formatter);
        using var eighth = ValidationBinding.ForValidationHelperProperty(view, helper, (current, _) => helperState = current, formatter);
        using var ninth = ValidationBinding.ForViewModel<TestView, TestViewModel, string>(view, value => aggregate = value, formatter);
        using var tenth = ValidationBinding.ForViewModel<TestView, TestViewModel, string>(view, target);
        using var eleventh = ValidationBinding.ForViewModel<TestView, TestViewModel, string>(view, target, formatter);
        await Assert.That(propertyState).IsSameReferenceAs(rich);
        await Assert.That(helperState).IsSameReferenceAs(rich);
        await Assert.That(((RichState)helperState!).Revision).IsEqualTo(FactoryStateRevision);
        await Assert.That(formatted![0]).IsEqualTo(SelectedFormat);
        await Assert.That(aggregate).IsEqualTo(SelectedFormat);
        await Assert.That(view.NameErrorLabel).IsEqualTo(SelectedFormat);
    }

    /// <summary>Normal predicate overloads capture their selected context and react through typed getters.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task FourNormalRuleOverloadsUseRegisteredTypedValues()
    {
        using var model = new TestViewModel { Name = "bad" };
        using var advisory = new ValidationContext(ImmediateSequencer.Instance);
        Expression<Func<TestViewModel, string?>> expression = vm => vm.Name;
        using var registry = new ValidationPlanRegistry(RuleRegistryCapacity);
        using var attachment = registry.Attach(model);
        using var lease = registry.RegisterSelector(ValidationPlanRole.RuleValue, expression, NameValue());
        using var second = model.ValidationRule(expression, static value => value == "ok", static value => value ?? "null");
        using var third = model.ValidationRule(advisory, expression, static value => value == "ok", "advisory");
        using var fourth = model.ValidationRule(advisory, expression, static value => value == "ok", static value => value ?? "null");
        using (var first = model.ValidationRule(expression, static value => value == "ok", ErrorMessage))
        {
            await Assert.That(first.IsValid).IsFalse();
            await Assert.That(third.IsValid).IsFalse();
            model.Name = "ok";
            await Assert.That(first.IsValid).IsTrue();
            await Assert.That(second.IsValid).IsTrue();
            await Assert.That(third.IsValid).IsTrue();
            await Assert.That(fourth.IsValid).IsTrue();
        }

        await Assert.That(model.ValidationContext.Validations.Count).IsEqualTo(1);
        await Assert.That(advisory.Validations.Count).IsEqualTo(AdvisoryRuleCount);
    }

    /// <summary>Unregistered normal calls fail before admitting rules or invoking callbacks.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task UnregisteredNormalRoutesFailActionably()
    {
        using var model = new TestViewModel();
        var view = new TestView(model);
        Expression<Func<TestViewModel, string?>> expression = vm => vm.Name;
        await Assert.That(() => model.ValidationRule(expression, static _ => true, ErrorMessage)).Throws<InvalidOperationException>();
        await Assert.That(() => view.BindValidationState(model, expression, static current => current.Count, static _ => { }, true)).Throws<InvalidOperationException>();
        await Assert.That(model.ValidationContext.Validations.Count).IsEqualTo(0);
    }

    /// <summary>Property bindings read metadata without evaluating the selected leaf.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task PropertyBindingNeverReadsLeafGetter()
    {
        using var model = new TestViewModel();
        var view = new TestView(model);
        using var states = new BehaviorSubject<IValidationState>(new RichState(LeafGetterStateRevision));
        using var rule = model.AddObservableRule(states, [nameof(TestViewModel.Name)]);
        Expression<Func<TestViewModel, string?>> expression = vm => vm.Name;
        var reads = 0;
        var metadata = new ValidationSelector<TestViewModel, string?>(source => new(
            () =>
            {
                reads++;
                throw new InvalidOperationException("The property getter must not be evaluated by a property binding.");
            },
            [ValidationDependency.PropertyChanged(() => source, nameof(TestViewModel.Name))],
            ValidationObservationOptions<string?>.Default,
            static () => [ValidationPath.Legacy(nameof(TestViewModel.Name))]));
        using var registry = new ValidationPlanRegistry(SmallRegistryCapacity);
        using var attachment = registry.Attach(view);
        using var lease = registry.RegisterSelector(ValidationPlanRole.Property, expression, metadata);
        var counts = new List<int>();
        using var normal = view.BindValidationState(model, expression, static current => current.Count, counts.Add, true);
        using var factory = ValidationBinding.ForProperty(view, expression, (current, _) => counts.Add(current.Count), new ConstFormatter("x"));
        model.Name = "changed";
        await Assert.That(reads).IsEqualTo(0);
        await Assert.That(counts.TrueForAll(static count => count == 1)).IsTrue();
    }

    /// <summary>A value descriptor without a metadata reader cannot silently evaluate the leaf for binding metadata.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task RuntimePropertyPlanRequiresExplicitMetadataReader()
    {
        using var model = new TestViewModel();
        var view = new TestView(model);
        Expression<Func<TestViewModel, string?>> expression = vm => vm.Name;
        using var registry = new ValidationPlanRegistry(SmallRegistryCapacity);
        using var attachment = registry.Attach(view);
        using var lease = registry.RegisterSelector(ValidationPlanRole.Property, expression, NameValue());
        await Assert.That(() => ValidationBinding.ForProperty(view, expression, static (_, _) => { }, new ConstFormatter("x")))
            .Throws<InvalidOperationException>();
    }

    /// <summary>A null model needs no descriptor until a selected model supplies its current provider.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task InitiallyNullModelResolvesItsNewModelProvider()
    {
        using var model = new TestViewModel();
        var view = new TestView();
        var rich = new RichState(SelectedModelStateRevision);
        using var richStates = new BehaviorSubject<IValidationState>(rich);
        using var helper = model.AddObservableRule(richStates, []);
        model.NameRule = helper;
        Expression<Func<TestViewModel, ValidationHelper?>> expression = vm => vm.NameRule;
        using var modelRegistry = new ValidationPlanRegistry(SmallRegistryCapacity);
        using var modelAttachment = modelRegistry.Attach(model);
        using var current = modelRegistry.RegisterSelector(ValidationPlanRole.Helper, expression, HelperSelector());
        IValidationState? observed = null;
        using var binding = view.BindValidationState(model, expression, static state => state, state => observed = state);
        await Assert.That(observed!.IsValid).IsTrue();
        view.ViewModel = model;
        await Assert.That(observed).IsSameReferenceAs(rich);
    }

    /// <summary>Factories resolve once for each selected model and use the selected model's provider first.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task ModelSelectionResolvesExactlyOneDescriptorFactory()
    {
        using var first = new TestViewModel();
        using var second = new TestViewModel();
        using var firstStates = new BehaviorSubject<IValidationState>(new RichState(FirstModelStateRevision));
        using var secondStates = new BehaviorSubject<IValidationState>(new RichState(SecondModelStateRevision));
        using var firstRule = first.AddObservableRule(firstStates, []);
        using var secondRule = second.AddObservableRule(secondStates, []);
        first.NameRule = firstRule;
        second.NameRule = secondRule;
        var view = new TestView(first);
        Expression<Func<TestViewModel, ValidationHelper?>> expression = vm => vm.NameRule;
        var factories = 0;
        var fallbackFactories = 0;
        using var modelRegistry = new ValidationPlanRegistry(SmallRegistryCapacity);
        using var firstAttachment = modelRegistry.Attach(first);
        using var secondAttachment = modelRegistry.Attach(second);
        using var modelLease = modelRegistry.RegisterSelectorPattern(
            ValidationPlanRole.Helper,
            "helper",
            ValidationExpressionPattern.Create(expression),
            _ =>
            {
                factories++;
                return HelperSelector();
            });
        using var viewRegistry = new ValidationPlanRegistry(SmallRegistryCapacity);
        using var viewAttachment = viewRegistry.Attach(view);
        using var viewLease = viewRegistry.RegisterSelectorPattern(
            ValidationPlanRole.Helper,
            "helper",
            ValidationExpressionPattern.Create(expression),
            _ =>
            {
                fallbackFactories++;
                return HelperSelector();
            });
        using var binding = view.BindValidationState(first, expression, static state => state.IsValid, static _ => { });
        await Assert.That(factories).IsEqualTo(1);
        view.ViewModel = second;
        await Assert.That(factories).IsEqualTo(SelectedModelCount);
        await Assert.That(fallbackFactories).IsEqualTo(0);
    }

    /// <summary>Structural rule membership follows changed metadata and preserves custom state objects.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task StructuralMetadataChangesRefreshAllRuleMembership()
    {
        using var model = new TestViewModel();
        var cell = new ValidationCell<int>(1);
        var firstPath = ValidationPath.Structural("Items[1]", 1, EqualityComparer<int>.Default);
        var secondPath = ValidationPath.Structural("Items[2]", SecondPropertyKey, EqualityComparer<int>.Default);
        var rich = new RichState(MetadataStateRevision);
        var selector = new ValidationSelector<ValidationCell<int>, int>(source => new(
            () => ValidationRead<int>.Present(source.Value, [source.Value == 1 ? firstPath : secondPath]),
            [ValidationDependency.PropertyChanged(() => source, nameof(source.Value))],
            ValidationObservationOptions<int>.Default));
        using var rule = ValidationRuntime.RegisterRule(cell, model.ValidationContext, selector, _ => rich);
        var contextSelector = new ValidationSelector<TestViewModel, IValidationContext?>(source => new(
            () => ValidationRead<IValidationContext?>.Present(source.ValidationContext, []),
            [],
            ValidationObservationOptions<IValidationContext?>.Default));
        var pathSelector = new ValidationSelector<TestViewModel, ValidationPath>(_ => new(
            () => ValidationRead<ValidationPath>.Present(secondPath, []),
            [],
            ValidationObservationOptions<ValidationPath>.Default));
        IList<IValidationState>? observed = null;
        using var binding = ValidationRuntime.Bind(ValidationRuntime.ObserveProperty(model, contextSelector, pathSelector, true, ValidationInitialSequence.Actual), current => observed = current);
        await Assert.That(observed!.Count).IsEqualTo(0);
        cell.Value = SecondPropertyKey;
        await Assert.That(observed!.Count).IsEqualTo(1);
        await Assert.That(observed[0]).IsSameReferenceAs(rich);
        cell.Value = 1;
        await Assert.That(observed!.Count).IsEqualTo(0);
    }

    /// <summary>Creates a typed Name getter with complete legacy metadata.</summary>
    /// <returns>The value descriptor.</returns>
    private static ValidationSelector<TestViewModel, string?> NameValue() => new(source => new(
        () => ValidationRead<string?>.Present(source.Name, [ValidationPath.Legacy(nameof(TestViewModel.Name))]),
        [ValidationDependency.PropertyChanged(() => source, nameof(TestViewModel.Name))],
        ValidationObservationOptions<string?>.Default));

    /// <summary>Creates a metadata descriptor whose leaf getter is unavailable.</summary>
    /// <returns>The property descriptor.</returns>
    private static ValidationSelector<TestViewModel, string?> NameMetadata() => new(source => new(
        static () => throw new InvalidOperationException("A metadata-only descriptor has no value getter."),
        [ValidationDependency.PropertyChanged(() => source, nameof(TestViewModel.Name))],
        ValidationObservationOptions<string?>.Default,
        static () => [ValidationPath.Legacy(nameof(TestViewModel.Name))]));

    /// <summary>Creates a typed helper getter and notification contract.</summary>
    /// <returns>The helper descriptor.</returns>
    private static ValidationSelector<TestViewModel, ValidationHelper?> HelperSelector() => new(source => new(
        () => ValidationRead<ValidationHelper?>.Present(source.NameRule, []),
        [ValidationDependency.PropertyChanged(() => source, nameof(TestViewModel.NameRule))],
        ValidationObservationOptions<ValidationHelper?>.Default));

    /// <summary>Creates a direct typed string assignment.</summary>
    /// <returns>The presentation descriptor.</returns>
    private static ValidationTarget<TestView, string> TextTarget() => new(view => new(
        () => ValidationTargetAccess<string>.Present(view, value => view.NameErrorLabel = value),
        []));

    /// <summary>A rich caller-created state whose identity must survive presentation.</summary>
    /// <param name="revision">The caller's metadata.</param>
    private sealed class RichState(int revision) : IValidationState
    {
        /// <inheritdoc/>
        public bool IsValid => false;

        /// <inheritdoc/>
        public IValidationText Text { get; } = ValidationText.Create(ErrorMessage);

        /// <summary>Gets the metadata retained independently of validity and text.</summary>
        public int Revision { get; } = revision;
    }
}
