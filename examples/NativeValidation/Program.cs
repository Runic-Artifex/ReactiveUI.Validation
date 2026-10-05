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
                Console.WriteLine($"{{\"case\":\"{name}\",\"passed\":false,\"exception\":\"{error.GetType().Name}\"}}");
                Console.Error.WriteLine($"{name}: {error}");
            }
        }
        return failed == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void GenericField()
    {
        using var customer = new Customer();
        Expression<Func<Customer, string?>> selector = model => model.Email;
        using var field = AttachField(customer, selector, static email => email?.Contains('@') == true, "email-required");
        Check(!field.IsValid && customer.HasErrors, "initial field errors");
        Check(customer.GetErrors(nameof(Customer.Email)).Cast<string>().Single() == "email-required", "field metadata");
        customer.Email = "owner@example.test";
        Check(field.IsValid && !customer.HasErrors, "notifying reusable field");
        field.Dispose();
        Check(customer.ValidationContext.Validations.Count == 0, "field owner removes registration");
    }

    private static ValidationHelper AttachField<TModel, TValue>(TModel owner, Expression<Func<TModel, TValue?>> selector, Func<TValue?, bool> predicate, string message)
        where TModel : class, IReactiveObject, IValidatableViewModel => owner.ValidationRule(selector, predicate, message);

    private static void NullableEditor()
    {
        using var first = new Customer { Address = new Address() };
        using var second = new Customer { Address = new Address { Postcode = "12345" } };
        using var firstRule = first.ValidationRule(model => model.Address!.Postcode, static postcode => postcode?.Length == 5, "postcode-required");
        using var secondRule = second.ValidationRule(model => model.Address!.Postcode, static postcode => postcode?.Length == 5, "postcode-required");
        first.AddressRule = firstRule;
        second.AddressRule = secondRule;
        var editor = new Editor { ViewModel = first };
        using var binding = editor.BindValidationState(first, model => model.AddressRule, view => view.Status, Project);
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
        second.AddressRule = null;
        Check(editor.Status is null, "null helper clears typed nullable target");
        editor.ViewModel = null;
        writes = editor.Writes;
        second.Address!.Postcode = "12345";
        Check(editor.Writes == writes, "null editor detaches");
        binding.Dispose();
        editor.ViewModel = first;
        Check(editor.Writes == writes, "owner binding cleanup");
        Check(nullAddressInvalid, "null nested address invalid (legacy observation retains the previous value)");
    }

    private static Presentation? Project(IValidationState state) => state.IsValid ? null : new Presentation(Severity.Blocking, state.Text.ToSingleLine(), 1);

    private static void CrossField()
    {
        using var customer = new Customer();
        using var advisory = new ValidationContext();
        var matching = new Current<bool>(false);
        void Match() => matching.Set(customer.Email.Length > 0 && customer.Email == customer.Confirmation);
        using var emailValues = new PropertyValues<string>(customer, nameof(Customer.Email), () => customer.Email).Subscribe(new Observer<string>(_ => Match()));
        using var confirmationValues = new PropertyValues<string>(customer, nameof(Customer.Confirmation), () => customer.Confirmation).Subscribe(new Observer<string>(_ => Match()));
        using var email = customer.ValidationRule(model => model.Email, matching, "emails-differ");
        using var confirmation = customer.ValidationRule(model => model.Confirmation, matching, "emails-differ");
        using var hint = customer.ValidationRule(advisory, model => model.Email, static value => value?.EndsWith("@company.test", StringComparison.Ordinal) == true, "prefer-work-email");
        Check(customer.HasErrors && !customer.ValidationContext.GetIsValid(), "blocking cross-field rule");
        Check(customer.GetErrors(nameof(Customer.Email)).Cast<string>().Single() == "emails-differ", "email metadata");
        Check(customer.GetErrors(nameof(Customer.Confirmation)).Cast<string>().Single() == "emails-differ", "confirmation metadata");
        customer.Email = "person@example.test";
        customer.Confirmation = customer.Email;
        Check(!customer.HasErrors && customer.ValidationContext.GetIsValid() && !advisory.GetIsValid(), "advisory is independent of command admission");
        hint.Dispose();
        Check(advisory.Validations.Count == 0 && customer.ValidationContext.Validations.Count == 2, "captured context ownership");
    }

    private static void Rows()
    {
        using var customer = new Customer();
        var rows = new Dictionary<int, (Current<IValidationState> Source, ValidationHelper Rule)>();
        for (int id = 1; id <= 2; id++)
        {
            var source = new Current<IValidationState>(new UniquenessState(false, "checking", 0));
            rows.Add(id, (source, customer.ValidationRule(source)));
        }
        Check(customer.HasErrors && customer.ValidationContext.Validations.Count == 2, "new rows block while uniqueness is pending");
        IValidationState? lastState = null;
        using var stateSubscription = rows[1].Rule.ValidationChanged.Subscribe(new Observer<IValidationState>(state => lastState = state));
        rows[1].Source.Set(new UniquenessState(false, "duplicate", 1));
        Check(!rows[1].Rule.IsValid && lastState is UniquenessState { Code: "duplicate", Revision: 1 }, "async result retains rich struct state");
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
}
