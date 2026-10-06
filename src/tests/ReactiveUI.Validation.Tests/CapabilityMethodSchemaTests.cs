// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Finite ordinary-method schemas preserve typed dispatch without invoking expression methods.</summary>
[ValidationRuntimeDispatch]
public class CapabilityMethodSchemaTests
{
    /// <summary>The schema identity for a known peer method.</summary>
    private const string MethodSchema = "peer-read";

    /// <summary>A competing schema identity.</summary>
    private const string CompetingSchema = "competing-read";

    /// <summary>The normal rule's invalid-state message.</summary>
    private const string RuleMessage = "method value is invalid";

    /// <summary>The original descriptor factory error.</summary>
    private const string FactoryMessage = "No typed validation capability is registered for RuleValue.";

    /// <summary>The bounded catalog and association capacity.</summary>
    private const int RegistryCapacity = 2;

    /// <summary>A fresh ordinary method matches typed constants without invoking methods or receiver getters.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task FreshOrdinaryMethodArgumentsMatchWithoutExecution()
    {
        using var model = new MethodModel();
        var first = new MethodCapture();
        var current = new MethodCapture { Offset = 1 };
        var pattern = ReadPattern();
        await Assert.That(pattern.Matcher(ReadExpression(first), out var initial)).IsTrue();
        await Assert.That(initial!.Get(pattern.Capture)).IsSameReferenceAs(first);
        await Assert.That(pattern.Matcher(ReadExpression(current), out var replacement)).IsTrue();
        await Assert.That(replacement!.Get(pattern.Capture)).IsSameReferenceAs(current);
        Expression<Func<MethodModel, int>> property = source => source.Number;
        Expression<Func<MethodModel, int>> overload = source => source.Read(current, 1);
        Expression<Func<MethodModel, int>> unknownClosure = source => source.Read(current);
        await Assert.That(pattern.Matcher(property, out _)).IsFalse();
        await Assert.That(pattern.Matcher(overload, out _)).IsFalse();
        await Assert.That(pattern.Matcher(unknownClosure, out _)).IsFalse();
        var host = new MethodHost(model);
        Expression<Func<MethodHost, MethodModel>> receiver = owner => owner.Model;
        var exemplar = ReadExpression(current);
        var call = (MethodCallExpression)exemplar.Body;
        var foreignReceiver = Expression.MakeMemberAccess(Expression.Constant(host), ((MemberExpression)receiver.Body).Member);
        var foreign = Expression.Lambda<Func<MethodModel, int>>(
            Expression.Call(foreignReceiver, call.Method, call.Arguments),
            exemplar.Parameters);
        await Assert.That(pattern.Matcher(foreign, out _)).IsFalse();
        await Assert.That(model.ReadCount).IsEqualTo(0);
        await Assert.That(first.ReadCount).IsEqualTo(0);
        await Assert.That(current.ReadCount).IsEqualTo(0);
        await Assert.That(host.ReadCount).IsEqualTo(0);
    }

    /// <summary>Original normal calls and a real method group bind independent live model and capture owners.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task NormalRulesAndMethodGroupsObserveOnlyTheirCurrentBorrowedOwners()
    {
        using var first = new MethodModel();
        using var second = new MethodModel { Number = 1 };
        var firstCapture = new MethodCapture();
        var secondCapture = new MethodCapture();
        var pattern = ReadPattern();
        using var registry = new ValidationPlanRegistry(RegistryCapacity);
        using var firstAssociation = registry.Attach(first);
        using var secondAssociation = registry.Attach(second);
        using var registration = registry.RegisterSelectorPattern(
            ValidationPlanRole.RuleValue,
            MethodSchema,
            pattern.Matcher,
            arguments => LiveSelector(arguments.Get(pattern.Capture)));
        Func<MethodModel, Expression<Func<MethodModel, int>>, Func<int, bool>, string, ValidationHelper> original =
            ValidatableViewModelExtensions.ValidationRule<MethodModel, int>;
        using (var secondRule = original(second, ReadExpression(secondCapture), static value => value > 0, RuleMessage))
        {
            using (var firstRule = first.ValidationRule(ReadExpression(firstCapture), static value => value > 0, RuleMessage))
            {
                await Assert.That(first.ValidationContext.GetIsValid()).IsFalse();
                await Assert.That(second.ValidationContext.GetIsValid()).IsTrue();
                firstCapture.Offset = 1;
                await Assert.That(first.ValidationContext.GetIsValid()).IsTrue();
                secondCapture.Offset = -1;
                await Assert.That(second.ValidationContext.GetIsValid()).IsFalse();
                await Assert.That(first.ListenerCount).IsEqualTo(1);
                await Assert.That(second.ListenerCount).IsEqualTo(1);
                await Assert.That(firstCapture.ListenerCount).IsEqualTo(1);
                await Assert.That(secondCapture.ListenerCount).IsEqualTo(1);
            }

            var reads = first.ReadCount;
            var captureReads = firstCapture.ReadCount;
            first.Number = 1;
            firstCapture.Offset = 0;
            await Assert.That(first.ReadCount).IsEqualTo(reads);
            await Assert.That(firstCapture.ReadCount).IsEqualTo(captureReads);
            await Assert.That(first.ListenerCount).IsEqualTo(0);
            await Assert.That(firstCapture.ListenerCount).IsEqualTo(0);
            await Assert.That(first.IsDisposed).IsFalse();
            secondCapture.Offset = 0;
            await Assert.That(second.ValidationContext.GetIsValid()).IsTrue();
        }

        var finalReads = second.ReadCount;
        second.Number = 0;
        secondCapture.Offset = 1;
        await Assert.That(second.ReadCount).IsEqualTo(finalReads);
        await Assert.That(second.ListenerCount).IsEqualTo(0);
        await Assert.That(secondCapture.ListenerCount).IsEqualTo(0);
        await Assert.That(second.IsDisposed).IsFalse();
    }

    /// <summary>Method schemas reject unknown closed slots, preserve factory failures and check ambiguity before factories.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task MethodCatalogFailuresAndUnregistrationStayCold()
    {
        using var model = new MethodModel();
        var capture = new MethodCapture();
        var pattern = ReadPattern();
        var expression = ReadExpression(capture);
        var request = new ValidationPlanRequest(ValidationPlanRole.RuleValue, string.Empty);
        var factories = 0;
        using var registry = new ValidationPlanRegistry(RegistryCapacity);
        using (var first = registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, MethodSchema, pattern.Matcher, Factory))
        {
            Expression<Func<MethodModel, long>> wrongSlot = source => source.ReadLong(capture);
            Expression<Func<MethodModel, int>> noMatch = source => source.Number;
            await Assert.That(registry.TryGetSelector(request, wrongSlot, out _)).IsFalse();
            await Assert.That(registry.TryGetSelector(request, noMatch, out _)).IsFalse();
            using var competing = registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, CompetingSchema, pattern.Matcher, Factory);
            await Assert.That(() => registry.TryGetSelector(request, expression, out _)).Throws<InvalidOperationException>();
            await Assert.That(factories).IsEqualTo(0);
        }

        var original = new InvalidOperationException(FactoryMessage);
        using (var failure = registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, MethodSchema, pattern.Matcher, _ => throw original))
        {
            var observed = CaptureFailure(() => registry.TryGetSelector(request, expression, out _));
            await Assert.That(observed).IsSameReferenceAs(original);
        }

        await Assert.That(registry.TryGetSelector(request, expression, out _)).IsFalse();
        await Assert.That(model.ReadCount).IsEqualTo(0);
        await Assert.That(capture.ReadCount).IsEqualTo(0);

        ValidationSelector<MethodModel, int> Factory(ValidationArguments _)
        {
            factories++;
            return LiveSelector(capture);
        }
    }

    /// <summary>Closed generic, static and overloaded method identities stay exact while unsupported nodes fail closed.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ClosedGenericAndStaticMethodIdentitiesCannotAlias()
    {
        using var model = new MethodModel();
        var capture = new MethodCapture();
        Expression<Func<MethodModel, int>> genericInt = source => source.ReadGeneric<int>(null!);
        Expression<Func<MethodModel, int>> genericLong = source => source.ReadGeneric<long>(null!);
        var genericPattern = ConstantPattern(genericInt, 0);
        await Assert.That(genericPattern.Matcher(ReplaceConstant(genericInt, capture, 0), out _)).IsTrue();
        await Assert.That(genericPattern.Matcher(ReplaceConstant(genericLong, capture, 0), out _)).IsFalse();
        Expression<Func<MethodModel, int>> staticCall = source => MethodModel.ReadStatic(source, null!);
        var staticPattern = ConstantPattern(staticCall, 1);
        await Assert.That(staticPattern.Matcher(ReplaceConstant(staticCall, capture, 1), out var payload)).IsTrue();
        await Assert.That(payload!.Get(staticPattern.Capture)).IsSameReferenceAs(capture);
        await Assert.That(staticPattern.Matcher(ReadExpression(capture), out _)).IsFalse();
        Expression<Func<MethodModel, int>> overload = source => source.Read(null!, 1);
        await Assert.That(ReadPattern().Matcher(ReplaceConstant(overload, capture, 0), out _)).IsFalse();
        Expression<Func<MethodModel, int>> unsupported = source => source.Number + 1;
        await Assert.That(ValidationExpressionPattern.Create(unsupported)(unsupported, out _)).IsFalse();
        await Assert.That(model.ReadCount).IsEqualTo(0);
        await Assert.That(capture.ReadCount).IsEqualTo(0);
    }

    /// <summary>Creates a bounded schema for one direct ordinary method and typed capture argument.</summary>
    /// <returns>The finite matcher and its typed token.</returns>
    private static (ValidationExpressionMatcher<MethodModel, int> Matcher, ValidationArgument<MethodCapture> Capture) ReadPattern()
    {
        Expression<Func<MethodModel, int>> exemplar = source => source.Read(null!);
        return ConstantPattern(exemplar, 0);
    }

    /// <summary>Creates a finite typed constant binding at a known method argument position.</summary>
    /// <param name="exemplar">The known method expression.</param>
    /// <param name="argumentIndex">The known capture position.</param>
    /// <returns>The matcher and its capture token.</returns>
    private static (ValidationExpressionMatcher<MethodModel, int> Matcher, ValidationArgument<MethodCapture> Capture) ConstantPattern(
        Expression<Func<MethodModel, int>> exemplar,
        int argumentIndex)
    {
        var call = (MethodCallExpression)exemplar.Body;
        var capture = new ValidationArgument<MethodCapture>();
        return (ValidationExpressionPattern.Create(exemplar, ValidationExpressionBinding.Constant((ConstantExpression)call.Arguments[argumentIndex], capture)), capture);
    }

    /// <summary>Creates a fresh direct-call expression with the current typed capture owner.</summary>
    /// <param name="capture">The borrowed current capture.</param>
    /// <returns>The fresh expression.</returns>
    private static Expression<Func<MethodModel, int>> ReadExpression(MethodCapture capture)
    {
        Expression<Func<MethodModel, int>> exemplar = source => source.Read(null!);
        return ReplaceConstant(exemplar, capture, 0);
    }

    /// <summary>Replaces one known constant using opaque member identity from a typed exemplar.</summary>
    /// <param name="exemplar">The finite typed method schema.</param>
    /// <param name="capture">The current capture owner.</param>
    /// <param name="argumentIndex">The schema's capture position.</param>
    /// <returns>The fresh typed expression without executing its body.</returns>
    private static Expression<Func<MethodModel, int>> ReplaceConstant(Expression<Func<MethodModel, int>> exemplar, MethodCapture capture, int argumentIndex)
    {
        var call = (MethodCallExpression)exemplar.Body;
        var arguments = new Expression[call.Arguments.Count];
        call.Arguments.CopyTo(arguments, 0);
        arguments[argumentIndex] = Expression.Constant(capture);
        return Expression.Lambda<Func<MethodModel, int>>(Expression.Call(call.Object, call.Method, arguments), exemplar.Parameters);
    }

    /// <summary>Binds current typed model and capture access with independently owned INPC registrations.</summary>
    /// <param name="capture">The borrowed live capture owner.</param>
    /// <returns>The cold reusable descriptor.</returns>
    private static ValidationSelector<MethodModel, int> LiveSelector(MethodCapture capture) => new(source => new(
        () => ValidationRead<int>.Present(source.Read(capture), [ValidationPath.Legacy(nameof(MethodModel.Number))]),
        [ValidationDependency.PropertyChanged(() => source, nameof(MethodModel.Number)), ValidationDependency.PropertyChanged(() => capture, nameof(MethodCapture.Offset))],
        ValidationObservationOptions<int>.Default));

    /// <summary>Captures the exact expected error without swallowing unrelated failures.</summary>
    /// <param name="operation">The synchronous catalog operation.</param>
    /// <returns>The original failure, or null if the operation succeeded.</returns>
    private static InvalidOperationException? CaptureFailure(Action operation)
    {
        try
        {
            operation();
            return null;
        }
        catch (InvalidOperationException error)
        {
            return error;
        }
    }

    /// <summary>A borrowed reactive model whose direct method and INPC registrations are counted.</summary>
    private sealed class MethodModel : ReactiveObject, IValidatableViewModel, INotifyPropertyChanged, IDisposable
    {
        /// <summary>The explicitly owned INPC event handlers.</summary>
        private PropertyChangedEventHandler? _changed;

        /// <inheritdoc/>
        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        /// <inheritdoc/>
        public IValidationContext ValidationContext { get; } = new ValidationContext(ImmediateSequencer.Instance);

        /// <summary>Gets the number of current owned model notification handlers.</summary>
        public int ListenerCount => _changed?.GetInvocationList().Length ?? 0;

        /// <summary>Gets whether the fixture owner has disposed its context.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Gets the number of actual direct-method reads.</summary>
        public int ReadCount { get; private set; }

        /// <summary>Gets or sets the current model argument and raises its notification.</summary>
        public int Number
        {
            get;
            set
            {
                field = value;
                _changed?.Invoke(this, new(nameof(Number)));
            }
        }

        /// <summary>Reads through a static method with an explicit model argument.</summary>
        /// <param name="source">The borrowed model argument.</param>
        /// <param name="capture">The borrowed typed capture.</param>
        /// <returns>The current combined value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadStatic(MethodModel source, MethodCapture capture) => source.Read(capture);

        /// <summary>Reads a known model and capture through an explicitly registered ordinary method.</summary>
        /// <param name="capture">The borrowed typed capture.</param>
        /// <returns>The current combined value.</returns>
        public int Read(MethodCapture capture)
        {
            ReadCount++;
            return Number + capture.Offset;
        }

        /// <summary>Reads a distinct overload with an additional argument.</summary>
        /// <param name="capture">The borrowed typed capture.</param>
        /// <param name="addition">The explicit additional argument.</param>
        /// <returns>The combined value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(MethodCapture capture, int addition) => Read(capture) + addition;

        /// <summary>Reads through a distinct closed output slot.</summary>
        /// <param name="capture">The borrowed typed capture.</param>
        /// <returns>The current combined long value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLong(MethodCapture capture) => Read(capture);

        /// <summary>Reads through an exact closed generic method identity.</summary>
        /// <typeparam name="T">The explicitly closed method argument type.</typeparam>
        /// <param name="capture">The borrowed typed capture.</param>
        /// <returns>The current value under the known closed method contract.</returns>
        public int ReadGeneric<T>(MethodCapture capture) => typeof(T) == typeof(int) ? Read(capture) : Read(capture) + 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            IsDisposed = true;
            ValidationContext.Dispose();
        }
    }

    /// <summary>A typed capture whose current value and owned notification handlers are counted.</summary>
    private sealed class MethodCapture : INotifyPropertyChanged
    {
        /// <summary>The currently owned capture notification handlers.</summary>
        private PropertyChangedEventHandler? _changed;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        /// <summary>Gets the number of current owned capture handlers.</summary>
        public int ListenerCount => _changed?.GetInvocationList().Length ?? 0;

        /// <summary>Gets the number of actual capture reads.</summary>
        public int ReadCount { get; private set; }

        /// <summary>Gets or sets the current capture value and raises its notification.</summary>
        public int Offset
        {
            get
            {
                ReadCount++;
                return field;
            }

            set
            {
                field = value;
                _changed?.Invoke(this, new(nameof(Offset)));
            }
        }
    }

    /// <summary>A receiver getter that must never be executed while matching a foreign receiver expression.</summary>
    /// <param name="model">The borrowed receiver.</param>
    private sealed class MethodHost(MethodModel model)
    {
        /// <summary>Gets the number of actual receiver getter executions.</summary>
        public int ReadCount { get; private set; }

        /// <summary>Gets the borrowed receiver and counts forbidden matching-time reads.</summary>
        public MethodModel Model
        {
            get
            {
                ReadCount++;
                return model;
            }
        }
    }
}
