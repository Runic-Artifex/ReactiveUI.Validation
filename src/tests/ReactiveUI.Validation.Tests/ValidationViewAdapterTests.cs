// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Capabilities;
#else
using ReactiveUI.Validation.Capabilities;
#endif

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Checks explicitly adapted platform and routed hosts without discovering fictional generic view interfaces.</summary>
[SuppressMessage("Usage", "SST2496:Repeated disposal", Justification = "These ownership regressions deliberately exercise early, repeated and reentrant disposal.")]
public class ValidationViewAdapterTests
{
    /// <summary>The notification count after two replacements.</summary>
    private const int TwoNotifications = 2;

    /// <summary>Checks direct typed access, synchronous initial callbacks, identity replacement and borrowed lifetimes.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task BorrowedHostModelReplacementAndSetterUseTypedOperations()
    {
        var original = new Model();
        var replacement = new Model();
        var host = new Host { Model = original };
        using var view = Adapt(host);
        var paths = new List<string?>();
        view.PropertyChanged += (_, args) => paths.Add(args.PropertyName);
        await Assert.That(view.Owner).IsSameReferenceAs(host);
        await Assert.That(view.ViewModel).IsSameReferenceAs(original);
        host.Model = replacement;
        host.Notify();
        await Assert.That(view.ViewModel).IsSameReferenceAs(replacement);
        await Assert.That(paths.Count).IsEqualTo(1);
        view.ViewModel = null;
        await Assert.That(host.Model).IsNull();
        await Assert.That(paths.Count).IsEqualTo(TwoNotifications);
        ((IViewFor)view).ViewModel = original;
        await Assert.That(host.Model).IsSameReferenceAs(original);
        var stale = host.Callback;
        view.Dispose();
        view.Dispose();
        stale?.Invoke();
        await Assert.That(host.RegistrationEnded).IsEqualTo(1);
        await Assert.That(host.Disposed).IsFalse();
        await Assert.That(original.Disposed).IsFalse();
        await Assert.That(replacement.Disposed).IsFalse();
    }

    /// <summary>Checks reentrant disposal before property-changed delivery and provider-read failure cleanup.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ReentrantDisposalAndReadFailureReleaseOnlyTheRegistration()
    {
        var host = new Host();
        using var view = Adapt(host);
        var changed = 0;
        view.PropertyChanging += (_, _) => view.Dispose();
        view.PropertyChanged += (_, _) => changed++;
        host.Model = new Model();
        host.Notify();
        await Assert.That(changed).IsEqualTo(0);
        await Assert.That(host.RegistrationEnded).IsEqualTo(1);
        await Assert.That(host.Disposed).IsFalse();

        var failingHost = new Host();
        using var failingView = Adapt(failingHost);
        var failure = new InvalidOperationException("host read");
        failingHost.ReadFailure = failure;
        await Assert.That(failingHost.Notify).Throws<InvalidOperationException>();
        await Assert.That(failingHost.RegistrationEnded).IsEqualTo(1);
        await Assert.That(failingHost.Disposed).IsFalse();
    }

    /// <summary>Creates an adapter with typed host operations and an explicit notification registration.</summary>
    /// <param name="host">The borrowed host.</param>
    /// <returns>The adapter.</returns>
    private static ValidationViewAdapter<Host, Model> Adapt(Host host) =>
        new(host, static value => value.Read(), static (value, model) => value.Write(model), static (value, changed) => value.Register(changed));

    /// <summary>A routed host intentionally implementing no generic view interface.</summary>
    private sealed class Host : IDisposable
    {
        /// <summary>Gets or sets the current untyped host model.</summary>
        internal object? Model { get; set; }

        /// <summary>Gets or sets the provider callback.</summary>
        internal Action? Callback { get; set; }

        /// <summary>Gets or sets an explicit typed-read failure.</summary>
        internal Exception? ReadFailure { get; set; }

        /// <summary>Gets or sets the number of ended notification registrations.</summary>
        internal int RegistrationEnded { get; set; }

        /// <summary>Gets a value indicating whether the borrowed host was disposed.</summary>
        internal bool Disposed { get; private set; }

        /// <inheritdoc/>
        public void Dispose() => Disposed = true;

        /// <summary>Reads the current typed model without reflection.</summary>
        /// <returns>The current model.</returns>
        /// <exception cref="Exception">The explicit failure is requested.</exception>
        internal Model? Read() => ReadFailure is null ? (Model?)Model : throw ReadFailure;

        /// <summary>Writes and notifies current host storage.</summary>
        /// <param name="model">The new model.</param>
        internal void Write(Model? model)
        {
            Model = model;
            Notify();
        }

        /// <summary>Raises an explicit provider notification.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Notify() => Callback?.Invoke();

        /// <summary>Registers a borrowed host callback, including its synchronous initial invocation.</summary>
        /// <param name="changed">The provider callback.</param>
        /// <returns>The owned handler lifetime.</returns>
        internal Registration Register(Action changed)
        {
            Callback += changed;
            changed();
            return new(this, changed);
        }
    }

    /// <summary>A model with value equality that must not hide reference replacement.</summary>
    private sealed class Model : IDisposable, IEquatable<Model>
    {
        /// <summary>Gets a value indicating whether the borrowed model was disposed.</summary>
        internal bool Disposed { get; private set; }

        /// <inheritdoc/>
        public void Dispose() => Disposed = true;

        /// <inheritdoc/>
        public bool Equals(Model? other) => other is not null;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Model;

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>Owns only the host notification handler.</summary>
    /// <param name="host">The borrowed host.</param>
    /// <param name="changed">The owned callback.</param>
    private sealed class Registration(Host host, Action changed) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            host.Callback -= changed;
            host.RegistrationEnded++;
        }
    }
}
