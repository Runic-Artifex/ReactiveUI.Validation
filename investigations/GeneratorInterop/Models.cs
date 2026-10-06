namespace GeneratorInterop;

internal sealed partial class Model : ReactiveValidationObject
{
    [Reactive] public partial string Email { get; set; }
    [Reactive] public partial Address? Address { get; set; }
    [Reactive] public partial ValidationHelper? Rule { get; set; }
    [Reactive] public partial IValidationContext? SelectedContext { get; set; }
    [ObservableAsProperty] public partial bool DerivedValid { get; }
    public Current<bool> DerivedValues { get; } = new(false);

    public Model() => _derivedValidHelper = DerivedValues.ToProperty(this, model => model.DerivedValid, initialValue: false);
    public void DisposeDerived() => _derivedValidHelper?.Dispose();
}

internal sealed partial class Address : ReactiveObject
{
    [Reactive] public partial string Postcode { get; set; }
}

internal sealed partial class View : ReactiveObject, IViewFor<Model>
{
    [Reactive] public partial Model? ViewModel { get; set; }
    [Reactive] public partial string Message { get; set; }
    [Reactive] public partial Presentation? Status { get; set; }
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
}

internal readonly record struct Presentation(string Code);

#if FIELD_EMAIL || FIELD_HELPER || FIELD_CONTEXT || COMMAND_METADATA || FIELD_OBSERVABLE
internal sealed partial class GeneratedOnlyModel : ReactiveValidationObject
{
    [Reactive] private string _email = "";
    [Reactive] private ValidationHelper? _rule;
    [Reactive] private IValidationContext? _selectedContext;
    [ReactiveCommand] private void Submit() { }
}
#endif

#if FIELD_TARGET
internal sealed partial class GeneratedOnlyView : ReactiveObject, IViewFor<Model>
{
    public Model? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    [Reactive] private string _message = "";
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
}
#endif

#if GENERATED_INTERFACE
[IReactiveObject]
internal sealed partial class Standalone : IValidatableViewModel, IDisposable
{
    public string Email { get; set; } = "";
    public IValidationContext ValidationContext { get; } = new ValidationContext();
    public void Dispose() => ValidationContext.Dispose();
}
#endif
