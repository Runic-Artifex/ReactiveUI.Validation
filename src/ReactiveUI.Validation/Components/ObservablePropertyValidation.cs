// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Components;
#else
namespace ReactiveUI.Validation.Components;
#endif

/// <summary>Associates explicit paths with an unchanged supplied state stream.</summary>
internal sealed class ObservablePropertyValidation : IPropertyValidationComponent, IDisposable
{
    /// <summary>The state component and its owned subscription.</summary>
    private readonly ObservableValidation<object, object> _validation;

    /// <summary>The ordinal, unique complete property paths.</summary>
    private readonly HashSet<string> _paths = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ObservablePropertyValidation"/> class.</summary>
    /// <param name="states">Caller-created states.</param>
    /// <param name="propertyPaths">Complete paths, or an empty sequence for a model-wide rule.</param>
    internal ObservablePropertyValidation(IObservable<IValidationState> states, IEnumerable<string> propertyPaths)
    {
        ArgumentExceptionHelper.ThrowIfNull(states);
        ArgumentExceptionHelper.ThrowIfNull(propertyPaths);
        foreach (var path in propertyPaths)
        {
            ValidatePath(path);
            _ = _paths.Add(path);
        }

        _validation = new(states);
    }

    /// <inheritdoc/>
    public int PropertyCount => _paths.Count;

    /// <inheritdoc/>
    public IEnumerable<string> Properties => _paths.AsEnumerable();

    /// <inheritdoc/>
    public IValidationText? Text => _validation.Text;

    /// <inheritdoc/>
    public bool IsValid => _validation.IsValid;

    /// <inheritdoc/>
    public IObservable<IValidationState> ValidationStatusChange => _validation.ValidationStatusChange;

    /// <inheritdoc/>
    public bool ContainsPropertyName(string propertyName, bool exclusively) =>
        _paths.Contains(propertyName) && (!exclusively || _paths.Count == 1);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _validation.Dispose();

    /// <summary>Rejects empty path segments and whitespace without discovering any member.</summary>
    /// <param name="path">The caller's complete property path.</param>
    /// <exception cref="ArgumentException">The path has an empty segment or whitespace.</exception>
    internal static void ValidatePath(string path)
    {
        ArgumentExceptionHelper.ThrowIfNullOrEmpty(path);
        if (path[0] == '.' || path[^1] == '.' || path.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Property paths must have nonempty dot-separated segments without whitespace.", nameof(path));
        }

        foreach (var character in path)
        {
            if (char.IsWhiteSpace(character))
            {
                throw new ArgumentException("Property paths cannot contain whitespace.", nameof(path));
            }
        }
    }
}
