namespace GeneratorInterop;

internal static partial class Program
{
    private static int Main(string[] args)
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        if (args.Contains("--require-aot") && RuntimeFeature.IsDynamicCodeSupported) return 2;
#if FIELD_EMAIL || FIELD_HELPER || FIELD_CONTEXT || COMMAND_METADATA || FIELD_OBSERVABLE
        using var generated = new GeneratedOnlyModel();
#endif
        using var first = new Model();
        using var second = new Model();
        var view = new View { ViewModel = first };
#if GENERATED_INTERFACE
        using var standalone = new Standalone();
        using var rejectedInterface = standalone.ValidationRule(model => model.Email, static value => !string.IsNullOrEmpty(value), "required");
#endif
#if FIELD_EMAIL
        using var rejected = generated.ValidationRule(model => model.Email, static value => !string.IsNullOrEmpty(value), "required");
#endif
#if FIELD_HELPER
        var helperView = new FieldModelView { ViewModel = generated };
        using var rejected = helperView.BindValidationState(generated, model => model.Rule, static state => state.IsValid, static _ => { });
#endif
#if FIELD_CONTEXT
        var contextView = new FieldModelView { ViewModel = generated };
        using var rejected = contextView.BindValidationContext(generated, model => model.SelectedContext, static _ => { });
#endif
#if FIELD_TARGET
        var generatedView = new GeneratedOnlyView { ViewModel = first };
        using var rejected = generatedView.BindValidation(first, model => model.Email, target => target.Message);
#endif
#if FIELD_OBSERVABLE
        using var suppliedField = generated.AddObservableRule(generated.WhenAnyValue(model => model.Email), static value => new ValidationState(!string.IsNullOrEmpty(value), "required"), ["Email"]);
        Require(!suppliedField.IsValid && generated.GetErrors("Email").Cast<string>().Single() == "required", "Binding predicts generated Email field property with initial invalid full metadata");
        generated.Email = "accepted";
        Require(suppliedField.IsValid && !generated.HasErrors, "Binding observation delivers generated field setter notifications to explicit observable validation");
        suppliedField.Dispose();
        Require(generated.ValidationContext.Validations.Count == 0, "observable workaround registration lifetime");
        Console.WriteLine("{\"case\":\"field-observable-workaround\",\"passed\":true}");
#endif
#if COMMAND_METADATA
        var result = new Current<bool>(false);
        using var rejected = generated.ValidationRule(model => model.SubmitCommand, result, "not-admitted");
#endif
        var suppliedControl = new Current<bool>(false);
        using (var metadataControl = first.ValidationRule(model => model.Email, suppliedControl, "supplied-only"))
        {
            Require(!metadataControl.IsValid && first.GetErrors("Email").Cast<string>().Single() == "supplied-only", "declared-selector safe supplied-observable overload needs no Validation interception");
            suppliedControl.Set(true);
            Require(metadataControl.IsValid && !first.HasErrors, "supplied metadata-only rule follows caller stream");
        }
        Require(suppliedControl.Subscribers == 0 && first.ValidationContext.Validations.Count == 0, "safe supplied-observable control releases registration");
        using var email = first.ValidationRule(model => model.Email, static value => !string.IsNullOrWhiteSpace(value), "email-required");
        first.Rule = email;
        using var text = view.BindValidation(first, model => model.Email, target => target.Message);
        using var typed = view.BindValidationState(first, model => model.Rule, target => target.Status, static state => state.IsValid ? null : new Presentation(state.Text.ToSingleLine()));
        Require(!email.IsValid && view.Message == "email-required" && view.Status?.Code == "email-required", "declared Reactive partial properties produce initial invalid rule and string/rich view assignments");
        first.Email = "owner@example.test";
        Require(email.IsValid && view.Message.Length == 0 && view.Status is null, "Reactive property setter notifications reach generated Validation getters/setters");
        first.Address = new Address { Postcode = "" };
        using var postcode = first.ValidationRule(model => model.Address!.Postcode, static value => value?.Length == 5, "postcode-required");
        var previous = first.Address;
        first.Address = new Address { Postcode = "12345" };
        Require(postcode.IsValid, "declared generated nested Address replacement updates");
        previous.Postcode = "old";
        Require(postcode.IsValid, "generated nested path detaches previous address");
        first.Address = null;
        Require(!postcode.IsValid, "generated nested null emits predicate-defined invalid state");
        using var derived = first.ValidationRule(model => model.DerivedValid, static value => value, "derived-invalid");
        Require(!derived.IsValid, "Binding ObservableAsProperty declared get-only property observes initial false");
        first.DerivedValues.Set(true);
        Require(derived.IsValid && first.DerivedValid, "Binding generated OAPH notifies Validation rule");
        using var advisory = new ValidationContext();
        using var advisoryRule = first.ValidationRule(advisory, model => model.Email, static value => value?.EndsWith("@company.test", StringComparison.Ordinal) == true, "prefer-company");
        first.SelectedContext = advisory;
        var states = new List<IValidationState>();
        using var context = view.BindValidationContext(first, model => model.SelectedContext, states.Add);
        Require(!states[^1].IsValid, "declared Reactive context selection works");
        using var replacementContext = new ValidationContext();
        var replacementStates = new Current<IValidationState>(new ValidationState(false, "replacement-context"));
        using var replacementContextRule = replacementContext.AddObservableRule(replacementStates, []);
        first.SelectedContext = replacementContext;
        Require(states[^1].Text.ToSingleLine() == "replacement-context", "declared Reactive context non-null replacement follows actual new state");
        int selectedCount = states.Count;
        first.Email = "owner@company.test";
        Require(states.Count == selectedCount, "old selected context detaches despite live rule update");
        first.SelectedContext = null;
        Require(states[^1].IsValid, "declared Reactive context null clears");
        selectedCount = states.Count;
        replacementStates.Set(ValidationState.Valid);
        Require(states.Count == selectedCount, "null context detaches replacement source");
        using var secondRule = second.ValidationRule(model => model.Email, static value => !string.IsNullOrWhiteSpace(value), "replacement-required");
        second.Rule = secondRule;
        view.ViewModel = second;
        Require(view.Message == "replacement-required" && view.Status?.Code == "replacement-required", "declared Reactive view model replacement follows source");
        first.Email = "detached";
        Require(view.Message == "replacement-required", "old generated model detached");
        second.Email = "accepted";
        Require(view.Status is null && view.Message.Length == 0, "replacement generated model delivers change");
        view.ViewModel = null;
        second.Email = "";
        Require(view.Status is null && view.Message.Length == 0, "null generated view model detaches");
        first.DisposeDerived(); second.DisposeDerived();
        Console.WriteLine("{\"case\":\"declared-partial-and-oaph\",\"passed\":true}");
        return 0;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

#if FIELD_HELPER || FIELD_CONTEXT
internal sealed class FieldModelView : ReactiveObject, IViewFor<GeneratedOnlyModel>
{
    public GeneratedOnlyModel? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (GeneratedOnlyModel?)value; }
}
#endif
