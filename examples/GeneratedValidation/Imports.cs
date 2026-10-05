global using System.Runtime.CompilerServices;
global using IReactiveObject = ReactiveUI.IReactiveObject;
#if REACTIVE_SHIM
global using CurrentScheduler = System.Reactive.Concurrency.CurrentThreadScheduler;
global using ReactiveUI.Reactive;
global using ReactiveUI.Reactive.Builder;
global using ReactiveUI.Validation.Reactive.Abstractions;
global using ReactiveUI.Validation.Reactive.Collections;
global using ReactiveUI.Validation.Reactive.Components;
global using ReactiveUI.Validation.Reactive.Components.Abstractions;
global using ReactiveUI.Validation.Reactive.Contexts;
global using ReactiveUI.Validation.Reactive.Extensions;
global using ReactiveUI.Validation.Reactive.Helpers;
global using ReactiveUI.Validation.Reactive.States;
#else
global using CurrentScheduler = ReactiveUI.Primitives.Concurrency.CurrentThreadSequencer;
global using ReactiveUI;
global using ReactiveUI.Builder;
global using ReactiveUI.Validation.Abstractions;
global using ReactiveUI.Validation.Collections;
global using ReactiveUI.Validation.Components;
global using ReactiveUI.Validation.Components.Abstractions;
global using ReactiveUI.Validation.Contexts;
global using ReactiveUI.Validation.Extensions;
global using ReactiveUI.Validation.Helpers;
global using ReactiveUI.Validation.States;
#endif
