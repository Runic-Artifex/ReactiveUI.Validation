// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Splat;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Tests typed registration, resolver fallback and formatter ownership.</summary>
public class ValidationTextFormatterRegistrationTests
{
    /// <summary>Verifies the real locator fallback and interface registration precedence.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task TypedConstantsUseInterfaceContractAndLastRegistrationWins()
    {
        using var resolver = new InstanceGenericFirstDependencyResolver();
        using var scope = resolver.WithResolver(false);
        var first = new SingleLineFormatter("first");
        var last = new SingleLineFormatter("last");

        await Assert.That(GeneratedValidationBindingSupport.ResolveFormatter(null)).IsSameReferenceAs(SingleLineFormatter.Default);

        ValidationTextFormatterRegistration.Register(AppLocator.CurrentMutable, first);
        ValidationTextFormatterRegistration.Register(AppLocator.CurrentMutable, last);

        await Assert.That(GeneratedValidationBindingSupport.ResolveFormatter(null)).IsSameReferenceAs(last);
        await Assert.That(GeneratedValidationBindingSupport.ResolveFormatter(first)).IsSameReferenceAs(first);
        await Assert.That(resolver.GetServices<SingleLineFormatter>()).IsEmpty();
    }

    /// <summary>Verifies deferred construction and the pinned resolver's constant versus transient disposal.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task FactoryConstructionIsDeferredAndLifetimesFollowTheResolver()
    {
        const int ExpectedConstructions = 2;
        var constant = new DisposableFormatter();
        var first = new DisposableFormatter();
        var last = new DisposableFormatter();
        var constructions = 0;
        using (var resolver = new InstanceGenericFirstDependencyResolver())
        {
            ValidationTextFormatterRegistration.Register(resolver, constant);
            ValidationTextFormatterRegistration.RegisterFactory(resolver, () =>
            {
                constructions++;
                return constructions == 1 ? first : last;
            });

            await Assert.That(constructions).IsEqualTo(0);
            await Assert.That(ValidationTextFormatterResolver.Resolve(resolver)).IsSameReferenceAs(first);
            await Assert.That(ValidationTextFormatterResolver.Resolve(resolver)).IsSameReferenceAs(last);
            await Assert.That(constructions).IsEqualTo(ExpectedConstructions);
        }

        await Assert.That(constant.Disposals).IsEqualTo(1);
        await Assert.That(first.Disposals).IsEqualTo(0);
        await Assert.That(last.Disposals).IsEqualTo(0);
        first.Dispose();
        last.Dispose();
    }

    /// <summary>Verifies null service entries fall back and factory failures are not mistaken for missing services.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task FactoryNullFallsBackAndFactoryErrorsPropagate()
    {
        using var resolver = new InstanceGenericFirstDependencyResolver();
        ValidationTextFormatterRegistration.RegisterFactory(resolver, static () => null!);
        await Assert.That(ValidationTextFormatterResolver.Resolve(resolver)).IsSameReferenceAs(SingleLineFormatter.Default);

        var error = new InvalidOperationException("formatter construction failed");
        ValidationTextFormatterRegistration.RegisterFactory(resolver, () => throw error);
        await Assert.That(() => ValidationTextFormatterResolver.Resolve(resolver)).Throws<InvalidOperationException>();
    }

    /// <summary>A formatter whose disposal records the actual resolver lifetime.</summary>
    private sealed class DisposableFormatter : IValidationTextFormatter<string>, IDisposable
    {
        /// <summary>Gets the number of explicit disposals.</summary>
        internal int Disposals { get; private set; }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Format(IValidationText? validationText) => "formatted";

        /// <inheritdoc/>
        public void Dispose() => Disposals++;
    }
}
