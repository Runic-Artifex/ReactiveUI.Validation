// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE in the project root.

global using BenchmarkDotNet.Attributes;
global using ReactiveUI.Primitives.Signals;
#if REACTIVE_SHIM
global using ReactiveUI.Reactive;
global using ReactiveUI.Reactive.Builder;
global using ReactiveUI.Validation.Reactive.Abstractions;
global using ReactiveUI.Validation.Reactive.Collections;
global using ReactiveUI.Validation.Reactive.Components.Abstractions;
global using ReactiveUI.Validation.Reactive.Contexts;
global using ReactiveUI.Validation.Reactive.Extensions;
global using ReactiveUI.Validation.Reactive.Helpers;
global using ReactiveUI.Validation.Reactive.States;
global using ImmediateScheduler = System.Reactive.Concurrency.ImmediateScheduler;
#else
global using ReactiveUI;
global using ReactiveUI.Builder;
global using ReactiveUI.Validation.Abstractions;
global using ReactiveUI.Validation.Collections;
global using ReactiveUI.Validation.Components.Abstractions;
global using ReactiveUI.Validation.Contexts;
global using ReactiveUI.Validation.Extensions;
global using ReactiveUI.Validation.Helpers;
global using ReactiveUI.Validation.States;
global using ImmediateScheduler = ReactiveUI.Primitives.Concurrency.ImmediateSequencer;
#endif
