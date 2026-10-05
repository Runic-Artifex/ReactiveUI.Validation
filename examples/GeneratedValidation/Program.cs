
internal static class Program
{
    private static int Main(string[] args)
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        if (args.Contains("--require-aot") && RuntimeFeature.IsDynamicCodeSupported) return 2;
        int failed = 0;
        foreach (var (name, run) in new (string, Action)[]
        {
            ("initial-field-and-text", InitialField),
            ("nested-null-and-replacement", NestedRule),
            ("helper-model-rich-state", HelperReplacement),
            ("context-replacement", ContextReplacement),
            ("nested-target-handoff", NestedTarget),
            ("property-state-membership", PropertyMembership),
            ("rows-owned-observable-results", Rows),
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
                Console.WriteLine($"{{\"case\":\"{name}\",\"passed\":false}}");
                Console.Error.WriteLine($"{name}: {error}");
            }
        }
        return failed == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void InitialField()
    {
        using var customer = new Customer();
        using var rule = customer.ValidationRule(model => model.Email, static value => value?.Contains('@') == true, "email-required");
        var editor = new Editor { ViewModel = customer };
        using var text = editor.BindValidation(customer, model => model.Email, view => view.Message);
        Check(!rule.IsValid && customer.HasErrors && editor.Message == "email-required" && editor.Messages.Count > 0 && editor.Messages.All(static value => value == "email-required"), "initial invalid field is immediately presented without empty prelude");
        Check(customer.GetErrors(nameof(Customer.Email)).Cast<string>().Single() == "email-required", "property metadata survives packaging");
        customer.Email = "owner@example.test";
        Check(rule.IsValid && !customer.HasErrors && editor.Message == "", "notifying field clears errors");
        using var dynamicRule = customer.ValidationRule(model => model.Confirmation, static value => !string.IsNullOrEmpty(value), static value => $"confirmation:{value}");
        Check(!dynamicRule.IsValid && customer.GetErrors(nameof(Customer.Confirmation)).Cast<string>().Single() == "confirmation:", "dynamic message overload");
        rule.Dispose();
        Check(customer.ValidationContext.Validations.Count == 1, "rule helper captures registration lifetime");
        // Exercise the other five text overloads from their packaged analyzer.
        using var propertyFormatter = editor.BindValidation(customer, model => model.Confirmation, view => view.Message, null);
        using var all = editor.BindValidation(customer, view => view.Message);
        using var allFormatter = editor.BindValidation(customer, view => view.Message, null);
        customer.AddressRule = dynamicRule;
        using var helper = editor.BindValidation(customer, model => model!.AddressRule, view => view.Message);
        using var helperFormatter = editor.BindValidation(customer, model => model!.AddressRule, view => view.Message, null);
        customer.Confirmation = "confirmed";
        Check(editor.Message == "", "all generated text overloads execute");
        var supplied = new Current<bool>(false);
        using var metadata = customer.ValidationRule(model => model.Metadata.Value, supplied, "external-value");
        Check(customer.GetErrors("Metadata.Value").Cast<string>().Single() == "external-value", "metadata-only expression does not require notifying property owners");
        supplied.Set(true);
        Check(metadata.IsValid, "existing supplied-observable overload remains unintercepted and safe");
    }

    private static void NestedRule()
    {
        using var customer = new Customer { Address = new Address() };
        using var rule = customer.ValidationRule(model => model.Address!.Postcode, static value => value?.Length == 5, "postcode-required");
        Check(!rule.IsValid && customer.HasErrors, "nested rule initially invalid");
        Check(customer.GetErrors("Address.Postcode").Cast<string>().Single() == "postcode-required", "complete nested metadata");
        var previous = customer.Address!;
        customer.Address = new Address { Postcode = "12345" };
        Check(rule.IsValid && !customer.HasErrors, "nested replacement evaluates immediately");
        previous.Postcode = "old";
        Check(rule.IsValid, "previous address detached");
        customer.Address = null;
        Check(!rule.IsValid && customer.HasErrors, "missing intermediate emits null and predicate defines required-address policy");
        customer.Address = new Address { Postcode = "54321" };
        Check(rule.IsValid, "recovery after null intermediate");
        rule.Dispose();
        customer.Address.Postcode = "disposed";
        Check(customer.ValidationContext.Validations.Count == 0 && !customer.HasErrors, "disposed generated rule cannot rejoin context");
    }

    private static Presentation? Project(IValidationState state) => state.IsValid ? null : new Presentation(Severity.Blocking, state.Text.ToSingleLine(), state is UniquenessState rich ? rich.Revision : 1);

    private static void HelperReplacement()
    {
        using var first = new Customer();
        using var second = new Customer();
        using var firstRule = first.ValidationRule(model => model.Email, static value => !string.IsNullOrEmpty(value), "required");
        first.AddressRule = firstRule;
        var editor = new Editor { ViewModel = first };
        using var binding = editor.BindValidationState(first, model => model.AddressRule, view => view.Status, Project);
        var delivered = new List<IValidationState>();
        using var callback = editor.BindValidationState(first, model => model.AddressRule, static state => state, delivered.Add);
        Check(editor.Status?.Code == "required" && delivered.Count > 0 && delivered.All(static state => !state.IsValid), "helper initial state has no synthetic valid prefix");
        var source = new Current<IValidationState>(new UniquenessState(false, "", 7));
        using var replacement = second.ValidationRule(source);
        second.AddressRule = replacement;
        editor.ViewModel = second;
        Check(editor.Status is { Code: "", Revision: 7 }, "empty text does not imply valid and custom struct metadata survives");
        int writes = editor.Writes;
        first.Email = "changed";
        Check(editor.Writes == writes, "old view model detached");
        IValidationState boxed = new UniquenessState(false, "", 8);
        source.Set(boxed);
        Check(editor.Status?.Revision == 8 && ReferenceEquals(delivered[^1], boxed), "message-equal metadata change retains original boxed identity");
        second.AddressRule = null;
        Check(editor.Status is null && delivered[^1].IsValid, "null helper clears typed nullable target");
        writes = editor.Writes;
        source.Set(new UniquenessState(false, "detached", 9));
        Check(editor.Writes == writes, "replaced helper detached");
        second.AddressRule = replacement;
        Check(editor.Status?.Revision == 9, "helper reattachment reads actual current state");
        editor.ViewModel = null;
        Check(editor.Status is null, "null model clears typed target");
        writes = editor.Writes;
        source.Set(ValidationState.Valid);
        Check(editor.Writes == writes, "null model detaches live helper");
        binding.Dispose();
        editor.ViewModel = second;
        Check(editor.Writes == writes, "disposed target binding stays detached");
    }

    private static void NestedTarget()
    {
        using var customer = new Customer();
        using var rule = customer.ValidationRule(model => model.Email, static value => !string.IsNullOrEmpty(value), "required");
        customer.AddressRule = rule;
        var editor = new Editor { ViewModel = customer };
        var original = editor.Panel!;
        using var text = editor.BindValidation(customer, model => model.Email, view => view.Panel!.Message);
        using var rich = editor.BindValidationState(customer, model => model.AddressRule, view => view.Panel!.Status, Project);
        Check(original.Message == "required" && original.Status?.Code == "required", "nested target initially receives latest output");
        var replacement = new Panel();
        editor.Panel = replacement;
        Check(replacement.Message == "required" && replacement.Status?.Code == "required", "equal-overriding replacement target immediately replays latest output");
        customer.Email = "valid";
        Check(replacement.Message == "" && replacement.Status is null && original.Message == "required", "old target detached");
        editor.Panel = null;
        customer.Email = "";
        var reattached = new Panel();
        editor.Panel = reattached;
        Check(reattached.Message == "required" && reattached.Status?.Code == "required", "null target retains latest source output for reattachment");
        text.Dispose();
        rich.Dispose();
        editor.Panel = new Panel { Message = "sentinel", Status = new Presentation(Severity.Advisory, "sentinel", 99) };
        customer.Email = "valid";
        customer.Email = "";
        Check(editor.Panel.Message == "sentinel" && editor.Panel.Status is { Revision: 99 }, "disposed nested target binding stays detached");
    }

    private static void ContextReplacement()
    {
        using var first = new Customer();
        using var second = new Customer();
        using var blocking = new ValidationContext();
        using var advisory = new ValidationContext();
        using var rule = first.ValidationRule(blocking, model => model.Email, static value => !string.IsNullOrEmpty(value), "blocking-required");
        using var hint = first.ValidationRule(advisory, model => model.Email, static value => value?.EndsWith("@company.test", StringComparison.Ordinal) == true, static value => $"advisory:{value}");
        first.SelectedContext = blocking;
        second.SelectedContext = advisory;
        var editor = new Editor { ViewModel = first };
        var aggregates = new List<IValidationState>();
        var selected = new List<IList<IValidationState>>();
        using var text = editor.BindValidationContext(first, model => model.SelectedContext, view => view.Message);
        using var propertyText = editor.BindValidationContext(first, model => model.SelectedContext, model => model.Email, view => view.Message, null, true);
        using var aggregate = editor.BindValidationContext(first, model => model.SelectedContext, aggregates.Add);
        using var property = editor.BindValidationContext(first, model => model.SelectedContext, model => model.Email, states => selected.Add(states.ToArray()), true);
        Check(editor.Message == "blocking-required" && !aggregates[^1].IsValid && selected[^1].Count == 1, "selected context initial presentation");
        Check(!first.HasErrors && first.ValidationContext.Validations.Count == 0, "explicit contexts remain independent of default admission");
        first.SelectedContext = advisory;
        Check(editor.Message == "advisory:" && selected[^1].Count == 1, "notifying context replacement");
        int count = selected.Count;
        rule.Dispose();
        Check(selected.Count == count, "old selected context membership detached");
        first.SelectedContext = null;
        Check(editor.Message == "" && aggregates[^1].IsValid && selected[^1].Count == 0, "null context clears all projections");
        editor.ViewModel = second;
        Check(editor.Message == "advisory:", "new view model selects its context");
        first.Email = "owner@company.test";
        Check(editor.Message == "" && advisory.GetIsValid(), "captured explicit rule still observes original owner");
        editor.ViewModel = null;
        Check(selected[^1].Count == 0 && aggregates[^1].IsValid, "null selected model clears");
    }

    private static void PropertyMembership()
    {
        using var customer = new Customer();
        using var initial = customer.ValidationRule(model => model.Email, static value => !string.IsNullOrEmpty(value), "email-required");
        var editor = new Editor { ViewModel = customer };
        var all = new List<IList<IValidationState>>();
        var strict = new List<IList<IValidationState>>();
        using var allBinding = editor.BindValidationState(customer, model => model.Email, static states => states.ToArray(), states => all.Add(states), false);
        using var strictBinding = editor.BindValidationState(customer, model => model.Email, static states => states.ToArray(), states => strict.Add(states), true);
        using var typed = editor.BindValidationState(customer, model => model.Email, view => view.Status, static states => states.Any(static state => !state.IsValid) ? new Presentation(Severity.Blocking, "property", states.Count) : (Presentation?)null, false);
        Check(all.Count > 0 && all.All(static states => states.Count == 1 && !states[0].IsValid), "property initial state has no valid prefix");
        var crossSource = new Current<IValidationState>(new UniquenessState(false, "emails-differ", 1));
        using var cross = customer.AddObservableRule(crossSource, [nameof(Customer.Email), nameof(Customer.Confirmation)]);
        Check(all[^1].Count == 2 && strict[^1].Count == 1 && editor.Status?.Revision == 2, "non-strict includes cross-field and strict excludes it");
        IValidationState delivered = new UniquenessState(false, "emails-differ", 2);
        crossSource.Set(delivered);
        Check(all[^1].Any(state => ReferenceEquals(state, delivered)), "property binding retains rich state identity");
        initial.Dispose();
        Check(all[^1].Count == 1 && strict[^1].Count == 0, "removal updates selected membership");
        cross.Dispose();
        Check(all[^1].Count == 0 && editor.Status is null, "empty membership emits empty list");
        using var later = customer.ValidationRule(model => model.Email, static value => value == "accepted", "later");
        Check(all[^1].Count == 1 && !all[^1][0].IsValid, "later rule joins with actual invalid state");
        editor.ViewModel = null;
        Check(all[^1].Count == 0 && strict[^1].Count == 0, "null model clears property membership");
        int count = all.Count;
        customer.Email = "accepted";
        Check(all.Count == count, "null model detaches property context");
    }

    private static void Rows()
    {
        using var customer = new Customer();
        var first = new Current<IValidationState>(new UniquenessState(false, "checking", 0));
        var second = new Current<IValidationState>(new UniquenessState(false, "checking", 0));
        using var firstRule = customer.ValidationRule(first);
        using var secondRule = customer.ValidationRule(second);
        Check(customer.HasErrors && customer.ValidationContext.Validations.Count == 2, "pending rows block admission");
        IValidationState? last = null;
        using var subscription = firstRule.ValidationChanged.Subscribe(new Observer<IValidationState>(value => last = value));
        IValidationState result = new UniquenessState(false, "duplicate", 4);
        first.Set(result);
        Check(ReferenceEquals(last, result), "observable async result keeps boxed custom state identity");
        subscription.Dispose();
        firstRule.Dispose();
        Check(first.Subscribers == 0 && customer.ValidationContext.Validations.Count == 1, "row removal releases observable subscription");
        first.Set(ValidationState.Valid);
        Check(customer.HasErrors, "late removed result cannot unblock current row");
        second.Set(ValidationState.Valid);
        Check(!customer.HasErrors, "current row completion admits owner");
        secondRule.Dispose();
        Check(customer.ValidationContext.Validations.Count == 0, "row owner removes registrations");
    }
}
