using System.Linq.Expressions;

internal static class Program
{
    private static int Main(string[] args)
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        if (args.Contains("--require-aot") && RuntimeFeature.IsDynamicCodeSupported) return 2;
        int failed = 0;
        foreach (var (name, run) in new (string, Action)[]
        {
            ("generic-field", GenericField),
            ("nullable-editor-rich-target", NullableEditor),
            ("blocking-advisory-cross-field", CrossField),
            ("rows-async-uniqueness", Rows),
#if SAFE_API
            ("existing-observable-foundation", ExistingObservableFoundation),
#endif
        })
        {
            try
            {
                run();
                Console.WriteLine($"{{\"case\":\"{name}\",\"passed\":true}}");
            }
            catch (Exception error)
            {
                failed++;
                var failureId = error.Data["failureId"] is string assertion ? assertion : "unexpected-exception";
                Console.WriteLine($"{{\"case\":\"{name}\",\"passed\":false,\"exception\":\"{error.GetType().Name}\",\"failureId\":\"{failureId}\"}}");
                Console.Error.WriteLine($"{name}: {error}");
            }
        }
        return failed == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message, string failureId = "assertion")
    {
        if (!condition)
        {
            var error = new InvalidOperationException(message);
            error.Data["failureId"] = failureId;
            throw error;
        }
    }

    private static void GenericField()
    {
        using var customer = new Customer();
#if SAFE_API
        var values = new PropertyValues<string>(customer, nameof(Customer.Email), () => customer.Email);
        using var field = AttachField(customer, values, [nameof(Customer.Email)], static email => email.Contains('@'), "email-required");
#else
        Expression<Func<Customer, string?>> selector = model => model.Email;
        using var field = AttachField(customer, selector, static email => email?.Contains('@') == true, "email-required");
#endif
        Check(!field.IsValid && customer.HasErrors, "initial field errors");
        Check(customer.GetErrors(nameof(Customer.Email)).Cast<string>().Single() == "email-required", "field metadata");
        customer.Email = "owner@example.test";
        Check(field.IsValid && !customer.HasErrors, "notifying reusable field");
        field.Dispose();
        Check(customer.ValidationContext.Validations.Count == 0, "field owner removes registration");
#if SAFE_API
        ApplicationAdapterChecks.Run();
#endif
    }

#if SAFE_API
    private static ValidationHelper AttachField<TModel, TValue>(TModel owner, IObservable<TValue> values, IEnumerable<string> paths, Func<TValue, bool> predicate, string message)
        where TModel : class, IValidatableViewModel => owner.AddObservableRule(values, value => new ValidationState(predicate(value), message), paths);
#else
    private static ValidationHelper AttachField<TModel, TValue>(TModel owner, Expression<Func<TModel, TValue?>> selector, Func<TValue?, bool> predicate, string message)
        where TModel : class, IReactiveObject, IValidatableViewModel => owner.ValidationRule(selector, predicate, message);
#endif

    private static void NullableEditor()
    {
        using var first = new Customer { Address = new Address() };
        using var second = new Customer { Address = new Address { Postcode = "12345" } };
#if SAFE_API
        // This application's policy requires an address. The stream deliberately
        // emits null when the intermediate object is absent, then validates it.
        using var firstRule = first.AddObservableRule(new NestedPostcodes(first), static postcode => new ValidationState(postcode?.Length == 5, "postcode-required"), ["Address.Postcode"]);
        using var secondRule = second.AddObservableRule(new NestedPostcodes(second), static postcode => new ValidationState(postcode?.Length == 5, "postcode-required"), ["Address.Postcode"]);
#else
        using var firstRule = first.ValidationRule(model => model.Address!.Postcode, static postcode => postcode?.Length == 5, "postcode-required");
        using var secondRule = second.ValidationRule(model => model.Address!.Postcode, static postcode => postcode?.Length == 5, "postcode-required");
#endif
        first.AddressRule = firstRule;
        second.AddressRule = secondRule;
        var editor = new Editor { ViewModel = first };
#if SAFE_API
        using var binding = new HelperSelections(editor).BindObservableValidationState(static helper => helper.ValidationChanged, Project, value => editor.Status = value);
#else
        using var binding = editor.BindValidationState(first, model => model.AddressRule, view => view.Status, Project);
#endif
        Check(editor.Status?.Severity == Severity.Blocking && editor.Writes == 1, "actual initial state without valid prelude");
        var oldAddress = first.Address!;
        first.Address = new Address { Postcode = "54321" };
        Check(editor.Status is null, "nested replacement valid");
        int writes = editor.Writes;
        oldAddress.Postcode = "old";
        Check(editor.Writes == writes, "old nested address detached");
        first.Address = null;
        bool nullAddressInvalid = editor.Status?.Severity == Severity.Blocking;
        editor.ViewModel = second;
        Check(editor.Status is null, "editor model replacement");
        writes = editor.Writes;
        first.Address = new Address();
        Check(editor.Writes == writes, "old editor model detached");
        second.Address!.Postcode = "bad";
        Check(editor.Status?.Severity == Severity.Blocking, "new model update");
        var remoteState = new Current<IValidationState>(new UniquenessState(false, "", 7));
#if SAFE_API
        using var replacementRule = second.AddObservableRule(remoteState, ["Address.Postcode"]);
#else
        using var replacementRule = second.ValidationRule(remoteState);
#endif
        second.AddressRule = replacementRule;
        Check(editor.Status is { Severity: Severity.Blocking, Code: "", Revision: 7 }, "empty text is still invalid; rich struct metadata projects");
        writes = editor.Writes;
        second.Address!.Postcode = "12345";
        Check(editor.Writes == writes, "replaced helper source detached");
        remoteState.Set(ValidationState.Valid);
        Check(editor.Status is null, "replacement helper valid projection");
        remoteState.Set(new UniquenessState(false, "before-null-helper", 8));
        Check(editor.Status?.Revision == 8, "live helper invalid before null");
        second.AddressRule = null;
        Check(editor.Status is null, "null helper clears typed nullable target");
        writes = editor.Writes;
        remoteState.Set(ValidationState.Valid);
        Check(editor.Writes == writes, "null helper detaches still-live source");
        second.AddressRule = replacementRule;
        remoteState.Set(new UniquenessState(false, "before-null-editor", 9));
        Check(editor.Status?.Revision == 9, "live editor invalid before null model");
        editor.ViewModel = null;
        Check(editor.Status is null, "null editor clears invalid live helper");
        writes = editor.Writes;
        remoteState.Set(ValidationState.Valid);
        second.Address!.Postcode = "changed-after-null";
        second.AddressRule = firstRule;
        Check(editor.Writes == writes, "null editor detaches");
        binding.Dispose();
        editor.ViewModel = first;
        Check(editor.Writes == writes, "owner binding cleanup");
        Check(nullAddressInvalid, "required-address policy violated: legacy observation retains the previous value", "required-address-policy");
    }

    private static Presentation? Project(IValidationState state) => state.IsValid ? null : new Presentation(Severity.Blocking, state.Text.ToSingleLine(), state is UniquenessState rich ? rich.Revision : 1);

    private static void CrossField()
    {
        using var customer = new Customer();
        using var advisory = new ValidationContext();
        var matching = new Current<bool>(false);
        void Match() => matching.Set(customer.Email.Length > 0 && customer.Email == customer.Confirmation);
        using var emailValues = new PropertyValues<string>(customer, nameof(Customer.Email), () => customer.Email).Subscribe(new Observer<string>(_ => Match()));
        using var confirmationValues = new PropertyValues<string>(customer, nameof(Customer.Confirmation), () => customer.Confirmation).Subscribe(new Observer<string>(_ => Match()));
#if SAFE_API
        using var email = customer.AddObservableRule(matching, static valid => new ValidationState(valid, "emails-differ"), [nameof(Customer.Email), nameof(Customer.Confirmation)]);
        using var hint = advisory.AddObservableRule(new PropertyValues<string>(customer, nameof(Customer.Email), () => customer.Email), static value => new ValidationState(value.EndsWith("@company.test", StringComparison.Ordinal), "prefer-work-email"), [nameof(Customer.Email)]);
        var selected = new Current<Customer?>(customer);
        var propertyStates = new List<IList<IValidationState>>();
        using var propertyBinding = selected.BindObservablePropertyValidationState(static model => model.ValidationContext, nameof(Customer.Email), static states => states, propertyStates.Add, false);
        Check(propertyStates.Count > 0 && propertyStates.All(static states => states.Count == 1 && !states[0].IsValid), "actual cross-field initial state without valid prefix");
        var exclusiveStates = new List<IList<IValidationState>>();
        using var exclusiveBinding = selected.BindObservablePropertyValidationState(static model => model.ValidationContext, nameof(Customer.Email), static states => states, exclusiveStates.Add, true);
        Check(exclusiveStates[^1].Count == 0, "strict property selection excludes multi-property rule");
#else
        using var email = customer.ValidationRule(model => model.Email, matching, "emails-differ");
        using var confirmation = customer.ValidationRule(model => model.Confirmation, matching, "emails-differ");
        using var hint = customer.ValidationRule(advisory, model => model.Email, static value => value?.EndsWith("@company.test", StringComparison.Ordinal) == true, "prefer-work-email");
#endif
        Check(customer.HasErrors && !customer.ValidationContext.GetIsValid(), "blocking cross-field rule");
        Check(customer.GetErrors(nameof(Customer.Email)).Cast<string>().Single() == "emails-differ", "email metadata");
        Check(customer.GetErrors(nameof(Customer.Confirmation)).Cast<string>().Single() == "emails-differ", "confirmation metadata");
        customer.Email = "person@example.test";
        customer.Confirmation = customer.Email;
        Check(!customer.HasErrors && customer.ValidationContext.GetIsValid() && !advisory.GetIsValid(), "advisory is independent of command admission");
        hint.Dispose();
#if SAFE_API
        Check(advisory.Validations.Count == 0 && customer.ValidationContext.Validations.Count == 1, "captured context ownership");
        selected.Set(null);
        Check(propertyStates[^1].Count == 0, "null property selection clears");
        int count = propertyStates.Count;
        customer.Confirmation = "changed";
        Check(propertyStates.Count == count, "null selection detaches old context");
#else
        Check(advisory.Validations.Count == 0 && customer.ValidationContext.Validations.Count == 2, "captured context ownership");
#endif
    }

    private static void Rows()
    {
        using var customer = new Customer();
        var rows = new Dictionary<int, (Current<IValidationState> Source, ValidationHelper Rule)>();
        for (int id = 1; id <= 2; id++)
        {
            var source = new Current<IValidationState>(new UniquenessState(false, "checking", 0));
#if SAFE_API
            rows.Add(id, (source, customer.AddObservableRule(source, [])));
#else
            rows.Add(id, (source, customer.ValidationRule(source)));
#endif
        }
        Check(customer.HasErrors && customer.ValidationContext.Validations.Count == 2, "new rows block while uniqueness is pending");
        IValidationState? lastState = null;
        using var stateSubscription = rows[1].Rule.ValidationChanged.Subscribe(new Observer<IValidationState>(state => lastState = state));
        IValidationState deliveredState = new UniquenessState(false, "duplicate", 1);
        rows[1].Source.Set(deliveredState);
        Check(!rows[1].Rule.IsValid && ReferenceEquals(lastState, deliveredState) && lastState is UniquenessState { Code: "duplicate", Revision: 1 }, "async result retains original boxed rich struct state");
        stateSubscription.Dispose();
        var removed = rows[1];
        removed.Rule.Dispose();
        rows.Remove(1);
        Check(removed.Source.Subscribers == 0 && customer.ValidationContext.Validations.Count == 1, "removed row cancels validation ownership");
        removed.Source.Set(ValidationState.Valid);
        Check(customer.HasErrors, "late removed result cannot unblock remaining row");
        rows[2].Source.Set(ValidationState.Valid);
        Check(!customer.HasErrors && customer.ValidationContext.GetIsValid(), "all current rows admitted");
        rows[2].Rule.Dispose();
        rows.Clear();
        Check(customer.ValidationContext.Validations.Count == 0, "owner removes all rows");
    }

#if SAFE_API
    private static void ExistingObservableFoundation()
    {
        using var customer = new Customer();
        using var context = new ValidationContext();
        using var scheduled = new ValidationContext(CurrentScheduler.Instance);
        var states = new Current<IValidationState>(new ValidationState(false, "initial"));
        using var component = new ObservableValidation<Customer, bool>(states);
        using var helper = new ValidationHelper(component);
        using var contextHelper = new ValidationHelper(context);
        context.Add(component);
        var contextStates = new List<IValidationState>();
        using var contextSubscription = context.ValidationStatusChange.Subscribe(new Observer<IValidationState>(contextStates.Add));
        var validity = new Current<bool>(false);
        // This old overload uses the literal expression only for property metadata,
        // while the caller supplies all observed values. Its narrowed annotations
        // are exercised independently of AddObservableRule.
        using var propertyRule = customer.ValidationRule(model => model.Email, validity, "observable-email");
        Check(!helper.IsValid && !contextHelper.IsValid && !context.IsValid && !contextStates[^1].IsValid, "existing observable constructors initial state");
        Check(customer.HasErrors && customer.GetErrors(nameof(Customer.Email)).Cast<string>().Single() == "observable-email", "existing metadata-only expression exports errors");
        states.Set(ValidationState.Valid);
        validity.Set(true);
        Check(helper.IsValid && contextHelper.IsValid && context.IsValid && context.GetIsValid() && contextStates[^1].IsValid, "existing helper and context presentation update");
        Check(!customer.HasErrors && propertyRule.IsValid && scheduled.IsValid, "existing RVO supplied observable updates");
        context.Remove(component);
        int count = contextStates.Count;
        states.Set(new ValidationState(false, "detached"));
        Check(contextStates.Count == count && context.GetIsValid(), "existing context component removal");
    }
#endif
}
