// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Capabilities;
using ReactiveUI.Validation.Reactive.ValidationBindings.Abstractions;
#else
using ReactiveUI.Validation.Capabilities;
using ReactiveUI.Validation.ValidationBindings.Abstractions;
#endif

/// <summary>Shared actual-package fixture proving selected access and exact read/write contracts.</summary>
internal static class AccessCompatibilityScenario
{
    /// <summary>Runs selected init, readonly-field and private-setter target replay through normal generated calls.</summary>
    internal static void Run()
    {
        using var customer = new Customer();
        using var rule = customer.ValidationRule(model => model.Email, static value => value?.Contains('@') == true, "access-required");
        var first = new DerivedAccessPanel();
        var view = new AccessView { ViewModel = customer, Target = first };
        using var initial = view.BindValidation(customer, model => model.Email, target => target.Target!.Initial);
        using var storage = view.BindValidation(customer, model => model.Email, target => target.Target!.Storage);
        using var privateSetter = view.BindValidation(customer, model => model.Email, target => target.Target!.PrivateSetter);
        Check(first.Initial == "access-required" && first.Storage == "access-required" && first.PrivateSetter == "access-required" && first.InitWrites > 0, "Selected bridges mutate the existing owner after construction and preserve virtual init dispatch.");

        customer.Email = "owner@example.test";
        Check(first.Initial is { Length: 0 } && first.Storage is { Length: 0 } && first.PrivateSetter is { Length: 0 }, "Selected targets accept later output changes.");
        view.Target = null;
        customer.Email = "invalid";
        Check(first.Initial is { Length: 0 } && first.Storage is { Length: 0 } && first.PrivateSetter is { Length: 0 }, "A missing target retains output without mutating the detached owner.");
        var second = new AccessPanel<string>();
        view.Target = second;
        Check(second.Initial == "access-required" && second.Storage == "access-required" && second.PrivateSetter == "access-required", "A newly attached owner receives cached output.");
        Check(first.Initial is { Length: 0 } && first.Storage is { Length: 0 } && first.PrivateSetter is { Length: 0 }, "Replacing the owner does not revisit old storage.");

        initial.Dispose();
        storage.Dispose();
        privateSetter.Dispose();
        customer.Email = "disposed@example.test";
        view.Target = new AccessPanel<string>();
        Check(second.Initial == "access-required" && view.Target.Initial is null && view.Target.Storage is null && view.Target.PrivateSetter is null, "Disposal detaches selected target writes and replay.");

        AutomaticPrivateAccess();
        ErasedReferenceOwners();
        ReadPostconditions();
        ImmutableFactories();
        AccessGenericLexicalView<string>.Run("prefix-invalid", "prefix-valid");
        AccessGenericLexicalView<object>.Run(new object(), new object());
        AccessNonpartialGenericFactory.Run();
    }

    /// <summary>Preserves original model-private access when the generated target host belongs to an unrelated view.</summary>
    private static void AutomaticPrivateAccess()
    {
        using var source = new AccessPrivateSourceModel();
        using var first = new AccessPrivateSourceModel();
        using var rule = source.ValidationRule(model => model.Email, static value => value?.Contains('@') == true, "private-required");
        source.SetHelper(rule);
        var view = new AccessPrivateSourceView { ViewModel = source, Owner = first };
        using var helper = source.BindHelper(view);
        using var context = source.BindContext(view);
        Check(first.HelperSnapshot == "private-required" && first.ContextSnapshot == "private-required", "Legal original private helper/context getters and ordinary private setters survive an unrelated generated view host.");
        source.Email = "private@example.test";
        Check(first.HelperSnapshot.Length == 0 && first.ContextSnapshot.Length == 0, "Automatic private bridges preserve later source notifications.");
        view.Owner = null;
        source.Email = "invalid";
        using var second = new AccessPrivateSourceModel();
        view.Owner = second;
        Check(first.HelperSnapshot.Length == 0 && first.ContextSnapshot.Length == 0 && second.HelperSnapshot == "private-required" && second.ContextSnapshot == "private-required", "Automatic private writes replay cached output only into the newly attached owner.");
    }

    /// <summary>Exercises .NET 10 reference-only signature erasure for actual private generic owner storage.</summary>
    private static void ErasedReferenceOwners()
    {
        using var customer = new Customer();
        using var rule = customer.ValidationRule(model => model.Email, static value => value?.Contains('@') == true, "erased-required");
        var view = new AccessErasedOwnerView { ViewModel = customer };
        using var initial = view.BindInitial(customer);
        using var storage = view.BindStorage(customer);
        Check(view.InitialSnapshot == "erased-required" && view.StorageSnapshot == "erased-required", "Exact erased private generic reference owner signatures mutate existing init and readonly storage.");
        customer.Email = "erased@example.test";
        Check(view.InitialSnapshot is { Length: 0 } && view.StorageSnapshot is { Length: 0 }, "Erased owner storage accepts later output.");
        view.Detach();
        customer.Email = "invalid";
        view.Attach();
        Check(view.InitialSnapshot == "erased-required" && view.StorageSnapshot == "erased-required" && view.DetachedInitial is { Length: 0 } && view.DetachedStorage is { Length: 0 }, "Erased reference owners replay cached output without revisiting detached storage.");
        initial.Dispose();
        storage.Dispose();
        customer.Email = "disposed@example.test";
        view.Detach();
        view.Attach();
        Check(view.InitialSnapshot is null && view.StorageSnapshot is null && view.DetachedInitial == "erased-required" && view.DetachedStorage == "erased-required", "Disposal removes erased owner assignment and replay.");
    }

    /// <summary>Preserves actual private getter and field output contracts in generated rule reads.</summary>
    private static void ReadPostconditions()
    {
        using var source = new AccessReadFlowSource();
        using var certain = source.CertainRule();
        using var returned = source.ReturnCertainRule();
        using var field = source.CertainStorageRule();
        using var possible = source.PossibleRule();
        using var possibleField = source.PossibleStorageRule();
        using var number = source.NumberRule();
        using var numberField = source.NumberStorageRule();
        Check(certain.IsValid && returned.IsValid && field.IsValid && possible.IsValid && possibleField.IsValid && number.IsValid && numberField.IsValid, "Exact NotNull reference and Nullable<int> reads retain actual values, while a MaybeNull getter delivers its initial null.");
        Check(source.PropertyReads > 0 && source.PossibleSnapshot is null, "The selected private getters execute and preserve an actual nullable result.");

        source.SetValue("");
        Check(!certain.IsValid && !returned.IsValid && !possible.IsValid && source.PossibleSnapshot is { Length: 0 }, "Private read postconditions preserve changed empty values rather than replacing or dropping them.");
        source.SetValue("changed");
        Check(certain.IsValid && returned.IsValid && !possible.IsValid && source.PossibleSnapshot == "changed", "Generated private getter reads observe subsequent notifications and their complete values.");
        Check(field.IsValid && possibleField.IsValid && number.IsValid && numberField.IsValid, "Exact field and nullable value reads preserve their independent storage.");

        certain.Dispose();
        returned.Dispose();
        possible.Dispose();
        var reads = source.PropertyReads;
        source.SetValue(null);
        Check(source.PropertyReads == reads, "Disposal detaches private getter rule reads.");
    }

    /// <summary>Proves private immutable value storage and nullable immutable reference targets use supplied typed factories.</summary>
    private static void ImmutableFactories()
    {
        var values = new Current<string>("typed-initial");
        var initial = new PrivatePresentation("construction", 7);
        var cell = new ValidationCell<PrivatePresentation>(initial);
        var lens = new ValidationLens<PrivatePresentation, string>(
            static storage => ValidationRead<string>.Present(storage.Message, [ValidationPath.Legacy(nameof(PrivatePresentation.Message))]),
            static (storage, value) => storage with { Message = value });
        using var binding = lens.Target().Bind(cell).Bind(values);
        Check(initial.Message == "construction" && cell.Value.Message == "typed-initial" && cell.Value.Revision == 7, "A supplied lens replaces private immutable struct storage in its actual stable cell.");
        cell.Value = new PrivatePresentation("external reset", 9);
        Check(cell.Value.Message == "typed-initial" && cell.Value.Revision == 9, "Immutable struct replacement preserves complete latest storage and replays output.");
        values.Set("typed-change");
        Check(cell.Value.Message == "typed-change" && cell.Value.Revision == 9, "Typed immutable replacement handles later values.");
        binding.Dispose();
        values.Set("disposed");
        Check(cell.Value.Message == "typed-change", "The supplied lens owns only its subscriptions.");

        var optional = new ValidationCell<ImmutablePanel?>(null);
        var target = new ValidationTarget<ValidationCell<ImmutablePanel?>, string>(owner => new(
            () => owner.Value is null
                ? ValidationTargetAccess<string>.Missing()
                : ValidationTargetAccess<string>.Present(owner,
                    value => owner.Update(current => current is null ? null : current with { Message = value }),
                    value => owner.Value?.Message == value),
            [ValidationDependency.PropertyChanged(() => owner, nameof(owner.Value))]));
        var projected = new Current<string>("cached while missing");
        using var optionalBinding = target.Bind(optional).Bind(projected);
        Check(optional.Value is null, "An immutable missing target skips assignment.");
        var constructed = new ImmutablePanel("construction", 11);
        optional.Value = constructed;
        var firstAssigned = optional.Value!;
        Check(constructed.Message == "construction" && firstAssigned.Message == "cached while missing" && firstAssigned.Revision == 11, "Initial immutable construction receives cached output through explicit replacement.");
        var detached = optional.Value;
        optional.Value = null;
        projected.Set("latest while missing");
        optional.Value = new ImmutablePanel("replacement", 13);
        var latestAssigned = optional.Value!;
        Check(detached!.Message == "cached while missing" && latestAssigned.Message == "latest while missing" && latestAssigned.Revision == 13, "Immutable replacement uses the latest owner and retains output across null.");
    }

    /// <summary>Checks actual owner mutation and replay behavior.</summary>
    /// <param name="condition">The required runtime condition.</param>
    /// <param name="message">The failing contract.</param>
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>A notifying stable reference owner whose generic target is replaceable.</summary>
    internal sealed class AccessView : ReactiveObject, IViewFor<Customer>
    {
        /// <inheritdoc />
        public Customer? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }

        /// <summary>Gets or sets the current stable target owner.</summary>
        public AccessPanel<string>? Target { get; set => this.RaiseAndSetIfChanged(ref field, value); }

        /// <inheritdoc />
        object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Customer?)value; }
    }

    /// <summary>Declares actual explicitly selected generic storage without inferred backing-field contracts.</summary>
    /// <typeparam name="T">The exact generic field/property storage type.</typeparam>
    internal class AccessPanel<T> where T : class
    {
        /// <summary>The explicitly selected existing readonly named storage.</summary>
        [GeneratedValidationAccess]
        public readonly T? Storage = default;

        /// <summary>Gets the explicitly selected existing init-only property.</summary>
        [GeneratedValidationAccess]
        public virtual T? Initial { get; init; }

        /// <summary>Gets the existing property whose actual private setter is selected.</summary>
        [GeneratedValidationAccess]
        public T? PrivateSetter { get; private set; }
    }

    /// <summary>Records actual virtual init dispatch through the selected generic base member.</summary>
    internal sealed class DerivedAccessPanel : AccessPanel<string>
    {
        /// <summary>Gets the number of init setter invocations on this actual owner.</summary>
        public int InitWrites { get; private set; }

        /// <inheritdoc />
        public override string? Initial { get; init { field = value; InitWrites++; } }
    }

    /// <summary>Private immutable struct state accessed only by a caller-supplied typed lens.</summary>
    /// <param name="Message">The selected presentation.</param>
    /// <param name="Revision">Unrelated state preserved on every replacement.</param>
    private readonly record struct PrivatePresentation(string Message, int Revision);

    /// <summary>Immutable reference state constructed and replaced through a supplied target factory.</summary>
    /// <param name="Message">The selected presentation.</param>
    /// <param name="Revision">Complete state retained across target replacement.</param>
    private sealed record ImmutablePanel(string Message, int Revision);
}

/// <summary>Declares exact selected private read postconditions for actual-package compiler and runtime proof.</summary>
internal sealed class AccessReadFlowSource : ReactiveValidationObject
{
    /// <summary>The current possibly null getter result.</summary>
    private string? _value;

    /// <summary>The actual instance value returned through the nullable getter contract.</summary>
    private readonly int _number = 42;

    /// <summary>The existing named field whose declared nullable type has a non-null read postcondition.</summary>
    [GeneratedValidationAccess]
    [global::System.Diagnostics.CodeAnalysis.NotNull]
    private readonly string? CertainStorage = "field";

    /// <summary>The existing named field whose nonnullable declared type permits nullable reads.</summary>
    [GeneratedValidationAccess]
    [global::System.Diagnostics.CodeAnalysis.MaybeNull]
    private readonly string PossibleStorage = "maybe field";

    /// <summary>The exact nullable value field whose actual read is present.</summary>
    [GeneratedValidationAccess]
    [global::System.Diagnostics.CodeAnalysis.NotNull]
    private readonly int? NumberStorage = 43;

    /// <summary>Gets the number of actual selected reference getter invocations.</summary>
    internal int PropertyReads { get; private set; }

    /// <summary>Gets the actual nullable value received by the rule predicate.</summary>
    internal string? PossibleSnapshot { get; private set; } = "unread";

    /// <summary>Gets a non-null value through the property's exact output contract.</summary>
    [GeneratedValidationAccess]
    [global::System.Diagnostics.CodeAnalysis.NotNull]
    private string? Certain { get { PropertyReads++; return _value ?? "certain"; } }

    /// <summary>Gets a non-null value through the selected getter's exact return contract.</summary>
    [GeneratedValidationAccess]
    private string? ReturnCertain
    {
        [return: global::System.Diagnostics.CodeAnalysis.NotNull]
        get { PropertyReads++; return _value ?? "returned"; }
    }

    /// <summary>Gets an actual possibly null value without strengthening its declared read contract.</summary>
    [GeneratedValidationAccess]
    [global::System.Diagnostics.CodeAnalysis.MaybeNull]
    private string Possible { get { PropertyReads++; return _value; } }

    /// <summary>Gets an actual nullable value whose read postcondition permits Value access.</summary>
    [GeneratedValidationAccess]
    [global::System.Diagnostics.CodeAnalysis.NotNull]
    private int? Number => _number;

    /// <summary>Invalidates the three changing private reference reads.</summary>
    /// <param name="value">The exact current getter value.</param>
    internal void SetValue(string? value)
    {
        this.RaiseAndSetIfChanged(ref _value, value, nameof(Possible));
        this.RaisePropertyChanged(nameof(Certain));
        this.RaisePropertyChanged(nameof(ReturnCertain));
    }

    /// <summary>Registers a reference read requiring the property's non-null output contract.</summary>
    /// <returns>The normally generated property rule.</returns>
    internal ValidationHelper CertainRule() => this.ValidationRule(model => (AccessReadLength)model.Certain, static value => value.Value > 0, "notnull-property");

    /// <summary>Registers the selected getter-return contract through a source-legal nullable property read.</summary>
    /// <returns>The normally generated getter-return rule.</returns>
    internal ValidationHelper ReturnCertainRule() => this.ValidationRule(model => model.ReturnCertain, static value => !string.IsNullOrEmpty(value), "notnull-return");

    /// <summary>Registers a field read requiring its non-null output contract.</summary>
    /// <returns>The normally generated field rule.</returns>
    internal ValidationHelper CertainStorageRule() => this.ValidationRule(model => (AccessReadLength)model.CertainStorage, static value => value.Value == 5, "notnull-field");

    /// <summary>Registers an actual nullable read and records the complete value delivered to its predicate.</summary>
    /// <returns>The normally generated nullable getter rule.</returns>
    internal ValidationHelper PossibleRule() => this.ValidationRule(model => model.Possible, value => { PossibleSnapshot = value; return value is null; }, "maybe-null-property");

    /// <summary>Registers an exact nullable field read without changing its stored value.</summary>
    /// <returns>The normally generated nullable field rule.</returns>
    internal ValidationHelper PossibleStorageRule() => this.ValidationRule(model => model.PossibleStorage, static value => value == "maybe field", "maybe-null-field");

    /// <summary>Registers a Nullable<int> getter read requiring its non-null output postcondition.</summary>
    /// <returns>The normally generated nullable value rule.</returns>
    internal ValidationHelper NumberRule() => this.ValidationRule(model => model.Number.Value, static value => value == 42, "notnull-value-property");

    /// <summary>Registers a Nullable<int> field read requiring its non-null output postcondition.</summary>
    /// <returns>The normally generated nullable field value rule.</returns>
    internal ValidationHelper NumberStorageRule() => this.ValidationRule(model => model.NumberStorage.Value, static value => value == 43, "notnull-value-field");

    /// <summary>Requires non-null input for the packed negative call to the actual emitted MaybeNull bridge.</summary>
    /// <param name="value">The required non-null input.</param>
    /// <returns>The input length.</returns>
    internal static int RequireNonNull(string value) => value.Length;
}

/// <summary>Requires an actual non-null string operand in normally generated selected read conversions.</summary>
/// <param name="Value">The actual string length.</param>
internal readonly record struct AccessReadLength(int Value)
{
    /// <summary>Converts the selected actual string using a non-null input contract.</summary>
    /// <param name="value">The value proven non-null by its read postcondition.</param>
    public static implicit operator AccessReadLength(string value) => new(value.Length);
}

/// <summary>Defines original legal private selectors and target setters outside the generated view's lexical scope.</summary>
internal sealed class AccessPrivateSourceModel : ReactiveValidationObject
{
    /// <summary>The helper storage returned through the private selector.</summary>
    private ValidationHelper? _helper;

    /// <summary>Gets or sets the validated source value.</summary>
    public string Email { get; set => this.RaiseAndSetIfChanged(ref field, value); } = "";

    /// <summary>Gets the helper through original legal model-private access.</summary>
    private ValidationHelper? Helper => _helper;

    /// <summary>Gets the context through original legal model-private access.</summary>
    private IValidationContext Context => ValidationContext;

    /// <summary>Gets or sets the ordinary private helper presentation target.</summary>
    private string HelperMessage { get; set; } = "";

    /// <summary>Gets or sets the ordinary private context presentation target.</summary>
    private string ContextMessage { get; set; } = "";

    /// <summary>Gets the actual helper target storage after generated assignment.</summary>
    internal string HelperSnapshot => HelperMessage;

    /// <summary>Gets the actual context target storage after generated assignment.</summary>
    internal string ContextSnapshot => ContextMessage;

    /// <summary>Supplies an actual helper while preserving the private selector contract.</summary>
    /// <param name="helper">The source's existing validation helper.</param>
    internal void SetHelper(ValidationHelper helper) => this.RaiseAndSetIfChanged(ref _helper, helper, nameof(Helper));

    /// <summary>Creates a normal binding with selectors legal in this model's original lexical scope.</summary>
    /// <param name="view">The unrelated target view host.</param>
    /// <returns>The generated helper binding.</returns>
    internal IDisposable BindHelper(AccessPrivateSourceView view) => view.BindValidation(this, model => model == null ? null : model.Helper, target => target.Owner!.HelperMessage);

    /// <summary>Creates a normal context binding with selectors legal in this model's original lexical scope.</summary>
    /// <param name="view">The unrelated target view host.</param>
    /// <returns>The generated context binding.</returns>
    internal IValidationBinding BindContext(AccessPrivateSourceView view) => view.BindValidationContext(this, model => model.Context, target => target.Owner!.ContextMessage);
}

/// <summary>Provides an unrelated notifying view host with replaceable private presentation owners.</summary>
internal sealed class AccessPrivateSourceView : ReactiveObject, IViewFor<AccessPrivateSourceModel>
{
    /// <inheritdoc />
    public AccessPrivateSourceModel? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }

    /// <summary>Gets or sets the actual target owner.</summary>
    public AccessPrivateSourceModel? Owner { get; set => this.RaiseAndSetIfChanged(ref field, value); }

    /// <inheritdoc />
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (AccessPrivateSourceModel?)value; }
}

/// <summary>Contains actual private generic reference storage accessible from its original typed lexical caller.</summary>
internal sealed partial class AccessErasedOwnerView : ReactiveObject, IViewFor<Customer>
{
    /// <summary>The previous existing private target retained to verify detached-owner semantics.</summary>
    private HiddenTarget<string>? _detached;

    /// <inheritdoc />
    public Customer? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }

    /// <summary>Gets or sets the actual private reference owner in the original legal lexical scope.</summary>
    private HiddenTarget<string>? Owner { get; set => this.RaiseAndSetIfChanged(ref field, value); } = new();

    /// <summary>Gets the current existing init storage.</summary>
    internal string? InitialSnapshot => Owner?.Initial;

    /// <summary>Gets the current existing readonly named field.</summary>
    internal string? StorageSnapshot => Owner?.Storage;

    /// <summary>Gets the old owner's init storage after detachment.</summary>
    internal string? DetachedInitial => _detached?.Initial;

    /// <summary>Gets the old owner's readonly named field after detachment.</summary>
    internal string? DetachedStorage => _detached?.Storage;

    /// <inheritdoc />
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Customer?)value; }

    /// <summary>Creates a normal selected init binding from the original private-owner lexical scope.</summary>
    /// <param name="customer">The validated source model.</param>
    /// <returns>The generated binding.</returns>
    internal IDisposable BindInitial(Customer customer) => this.BindValidation(customer, model => model.Email, target => target.Owner!.Initial);

    /// <summary>Creates a normal selected readonly-field binding from the original private-owner lexical scope.</summary>
    /// <param name="customer">The validated source model.</param>
    /// <returns>The generated binding.</returns>
    internal IDisposable BindStorage(Customer customer) => this.BindValidation(customer, model => model.Email, target => target.Owner!.Storage);

    /// <summary>Detaches the current target while retaining its actual storage for assertions.</summary>
    internal void Detach()
    {
        _detached = Owner;
        Owner = null;
    }

    /// <summary>Constructs and attaches a new existing private owner.</summary>
    internal void Attach() => Owner = new();

    /// <summary>Declares exact existing storage with declaring type generic VAR constraints.</summary>
    /// <typeparam name="T">The exact actual storage value.</typeparam>
    private sealed class HiddenTarget<T> where T : class
    {
        /// <summary>The explicitly selected actual readonly named field.</summary>
        [GeneratedValidationAccess]
        internal readonly T? Storage = default;

        /// <summary>Gets the explicitly selected actual init-only property.</summary>
        [GeneratedValidationAccess]
        internal T? Initial { get; init; }
    }
}

/// <summary>Exercises all private reference signature positions with an exact enclosing generic VAR prefix.</summary>
/// <typeparam name="TPrefix">The actual enclosing CLR generic argument.</typeparam>
internal sealed partial class AccessGenericLexicalView<TPrefix> : ReactiveValidationObject, IViewFor<Customer>
    where TPrefix : class
{
    private readonly TPrefix _invalidPrefix;
    private readonly TPrefix _validPrefix;
    private readonly Marker _invalidMarker = new();
    private readonly Marker _validMarker = new();
    private Owner<Marker>? _owner = new();
    private Marker _key = new();

    private AccessGenericLexicalView(Customer model, TPrefix invalidPrefix, TPrefix validPrefix)
    {
        ViewModel = model;
        _invalidPrefix = invalidPrefix;
        _validPrefix = validPrefix;
    }

    /// <inheritdoc />
    public Customer? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }

    /// <inheritdoc />
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Customer?)value; }

    private Owner<Marker>? CurrentOwner { get => _owner; set => this.RaiseAndSetIfChanged(ref _owner, value); }
    private Marker Key { get { KeyReads++; return _key; } }
    private int KeyReads { get; set; }

    private IDisposable BindPrefix(Customer model) => this.BindValidationState(model, source => source.AddressRule,
        view => view.CurrentOwner!.Initial, state => state.IsValid ? _validPrefix : _invalidPrefix);

    private IDisposable BindValue(Customer model) => this.BindValidationState(model, source => source.AddressRule,
        view => view.CurrentOwner!.Value, state => state.IsValid ? _validMarker : _invalidMarker);

    private IDisposable BindStorage(Customer model) => this.BindValidationState(model, source => source.AddressRule,
        view => view.CurrentOwner!.Storage, state => state.IsValid ? _validMarker : _invalidMarker);

    private IDisposable BindIndex(Customer model) => this.BindValidationState(model, source => source.AddressRule,
        view => view.CurrentOwner![view.Key], static state => state.IsValid ? null : "indexed-required");

    private ValidationHelper ReadValue() => this.ValidationRule(view => view.CurrentOwner!.Value,
        value => ReferenceEquals(value, _validMarker), "private-generic-return");

    private ValidationHelper ReadStorage() => this.ValidationRule(view => view.CurrentOwner!.Storage,
        value => ReferenceEquals(value, _validMarker), "private-generic-field-return");

    /// <summary>Runs closed prefix/suffix, private constraint, nullable index, replay and ownership checks.</summary>
    /// <param name="invalidPrefix">The exact initial prefix value.</param>
    /// <param name="validPrefix">The exact later prefix value.</param>
    internal static void Run(TPrefix invalidPrefix, TPrefix validPrefix)
    {
        using var model = new Customer();
        using var rule = model.ValidationRule(source => source.Email, static value => value?.Contains('@') == true, "generic-required");
        model.AddressRule = rule;
        using var view = new AccessGenericLexicalView<TPrefix>(model, invalidPrefix, validPrefix);
        using var prefix = view.BindPrefix(model);
        using var value = view.BindValue(model);
        using var storage = view.BindStorage(model);
        using var indexed = view.BindIndex(model);
        using var returned = view.ReadValue();
        using var field = view.ReadStorage();
        var first = Present(view._owner, "The private generic owner is absent.");
        Require(ReferenceEquals(first.PrefixSnapshot, invalidPrefix) && ReferenceEquals(first.ValueSnapshot, view._invalidMarker)
            && ReferenceEquals(first.Storage, view._invalidMarker) && first.IndexSnapshot(view._key) == "indexed-required",
            "Private prefix/suffix init, value, readonly and indexed signatures retain exact initial arguments.");
        Require(!returned.IsValid && !field.IsValid && first.ValueReads > 0 && first.InitialWrites > 0,
            "Private generic return and byref field return deliver the actual initial marker.");
        var keyReads = view.KeyReads;
        model.Email = "generic@example.test";
        Require(view.KeyReads > keyReads && ReferenceEquals(first.PrefixSnapshot, validPrefix)
            && ReferenceEquals(first.ValueSnapshot, view._validMarker) && ReferenceEquals(first.Storage, view._validMarker)
            && first.IndexSnapshot(view._key) == "cleared" && ReferenceEquals(first.LastKey, view._key),
            "Source notifications preserve exact private marker identity and nullable setter input and refresh the index key on each output.");
        view.RaisePropertyChanged(nameof(CurrentOwner));
        Require(returned.IsValid && field.IsValid, "Private getter and field reads observe the new actual stored markers.");
        var oldKey = view._key;
        view._key = new Marker();
        view.RaisePropertyChanged(nameof(Key));
        Require(first.IndexSnapshot(view._key) == "cleared" && first.IndexSnapshot(oldKey) == "cleared"
            && ReferenceEquals(first.LastKey, view._key), "A new private index argument receives cached output without altering old keyed storage.");
        view.CurrentOwner = null;
        keyReads = view.KeyReads;
        model.Email = "invalid";
        Require(view.KeyReads == keyReads && ReferenceEquals(first.ValueSnapshot, view._validMarker)
            && ReferenceEquals(first.Storage, view._validMarker), "A missing owner caches output without evaluating a private key or revisiting detached storage.");
        view.CurrentOwner = new();
        var second = Present(view._owner, "The replacement private generic owner is absent.");
        Require(ReferenceEquals(second.PrefixSnapshot, invalidPrefix) && ReferenceEquals(second.ValueSnapshot, view._invalidMarker)
            && ReferenceEquals(second.Storage, view._invalidMarker) && second.IndexSnapshot(view._key) == "indexed-required"
            && ReferenceEquals(second.LastKey, view._key) && !returned.IsValid && !field.IsValid,
            "Replacement closes the same private constraints and replays all cached typed output into only the new owner.");
        prefix.Dispose(); value.Dispose(); storage.Dispose(); indexed.Dispose(); returned.Dispose(); field.Dispose();
        var reads = second.ValueReads;
        var writes = second.TotalWrites;
        keyReads = view.KeyReads;
        model.Email = "disposed@example.test";
        view.CurrentOwner = null;
        view.CurrentOwner = new();
        var disposedOwner = Present(view._owner, "The disposed replacement owner is absent.");
        Require(second.ValueReads == reads && second.TotalWrites == writes && view.KeyReads == keyReads
            && disposedOwner.ValueSnapshot is null && disposedOwner.Storage is null && disposedOwner.PrefixSnapshot is null
            && disposedOwner.TotalWrites == 0 && !view.ValidationContext.Validations.Items.Any(),
            "Disposal removes private generic reads, writes, key evaluation and replacement replay.");
    }

    private static T Present<T>(T? value, string message) where T : class => value ?? throw new InvalidOperationException(message);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private class ConstraintBase { }

    private sealed class Marker : ConstraintBase
    {
        public Marker() { }
    }

    private sealed class Owner<U> : ReactiveObject where U : ConstraintBase, new()
    {
        private TPrefix? _initial;
        private U? _value;
        private readonly Dictionary<U, string> _indexed = [];

        [GeneratedValidationAccess]
        internal TPrefix? Initial
        {
            get => _initial;
            init { _initial = value; InitialWrites++; this.RaisePropertyChanged(nameof(Initial)); }
        }

        [GeneratedValidationAccess]
        internal U? Value
        {
            get { ValueReads++; return _value; }
            init { _value = value; ValueWrites++; this.RaisePropertyChanged(nameof(Value)); }
        }

        [GeneratedValidationAccess]
        internal readonly U? Storage = default;

        [GeneratedValidationAccess]
        internal string this[U key]
        {
            get => IndexSnapshot(key);
            [param: global::System.Diagnostics.CodeAnalysis.AllowNull]
            set { _indexed[key] = value ?? "cleared"; LastKey = key; IndexWrites++; }
        }

        internal int InitialWrites { get; private set; }
        internal int ValueWrites { get; private set; }
        internal int IndexWrites { get; private set; }
        internal int ValueReads { get; private set; }
        internal int TotalWrites => InitialWrites + ValueWrites + IndexWrites;
        internal TPrefix? PrefixSnapshot => _initial;
        internal U? ValueSnapshot => _value;
        internal U? LastKey { get; private set; }
        internal string IndexSnapshot(U key) => _indexed.TryGetValue(key, out var value) ? value : "unwritten";
    }
}

/// <summary>Executes the legal typed replacement route when a private generic owner cannot host an intrinsic.</summary>
internal static class AccessNonpartialGenericFactory
{
    /// <summary>Preserves whole private generic storage and its lifetime through a supplied lens.</summary>
    internal static void Run()
    {
        var values = new Current<string>("first");
        var original = new Storage<string>(null, null, 7);
        var cell = new ValidationCell<Storage<string>?>(original);
        var lens = new ValidationLens<Storage<string>?, string>(
            static storage => storage is null ? ValidationRead<string>.Missing([])
                : ValidationRead<string>.Present(storage.Initial ?? "", [ValidationPath.Legacy(nameof(Storage<string>.Initial))]),
            static (storage, value) => storage is null ? null : new Storage<string>(value, value, storage.Marker));
        using var binding = lens.Target().Bind(cell).Bind(values);
        var first = cell.Value ?? throw new InvalidOperationException("The typed private generic storage is absent.");
        Require(original.Initial is null && original.Value is null && first.Initial == "first" && first.Value == "first"
            && first.Marker == 7 && values.Subscribers == 1, "The nonpartial factory constructs new private generic storage and owns one value subscription.");
        cell.Value = null;
        values.Set("cached");
        cell.Value = new(null, null, 11);
        var replayed = cell.Value ?? throw new InvalidOperationException("The replayed private generic storage is absent.");
        Require(replayed.Initial == "cached" && replayed.Value == "cached" && replayed.Marker == 11
            && first.Initial == "first", "Missing nonpartial storage caches output and replays through complete replacement without changing a stale snapshot.");
        values.Set("updated");
        var updated = cell.Value ?? throw new InvalidOperationException("The updated private generic storage is absent.");
        Require(updated.Initial == "updated" && updated.Value == "updated" && updated.Marker == 11
            && replayed.Initial == "cached", "Later output preserves the private generic marker while replacing only current storage.");
        binding.Dispose();
        values.Set("disposed");
        cell.Value = new(null, null, 13);
        var disposedStorage = cell.Value ?? throw new InvalidOperationException("The disposed private generic storage is absent.");
        Require(disposedStorage.Initial is null && disposedStorage.Value is null && disposedStorage.Marker == 13 && values.Subscribers == 0,
            "Disposal detaches the typed nonpartial factory and stops replacement replay.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Storage<T>(T? initial, T? value, int marker) where T : class
    {
        internal T? Initial { get; init; } = initial;
        internal readonly T? Value = value;
        internal readonly int Marker = marker;
    }
}
