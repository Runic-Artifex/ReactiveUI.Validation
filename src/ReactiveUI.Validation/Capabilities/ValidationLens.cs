// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A typed read and outward write-back contract for stable or immutable storage.</summary>
/// <typeparam name="TStorage">The current storage value.</typeparam>
/// <typeparam name="TValue">The selected member value.</typeparam>
[System.Diagnostics.DebuggerDisplay("ValidationLens")]
public sealed class ValidationLens<TStorage, TValue>
{
    /// <summary>The bound _read contract.</summary>
    private readonly Func<TStorage, ValidationRead<TValue>> _read;

    /// <summary>The bound _write contract.</summary>
    private readonly Func<TStorage, TValue, TStorage> _write;

    /// <summary>The optional metadata-only getter.</summary>
    private readonly Func<TStorage, IReadOnlyList<ValidationPath>>? _paths;

    /// <summary>Initializes a new instance of the <see cref="ValidationLens{TStorage,TValue}"/> class.</summary>
    /// <param name="read">Reads the selected value and metadata.</param>
    /// <param name="write">Writes the member and returns the complete replacement storage value.</param>
    public ValidationLens(Func<TStorage, ValidationRead<TValue>> read, Func<TStorage, TValue, TStorage> write)
    {
        ArgumentExceptionHelper.ThrowIfNull(read);
        ArgumentExceptionHelper.ThrowIfNull(write);
        _read = read;
        _write = write;
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationLens{TStorage,TValue}"/> class.</summary>
    /// <param name="read">Reads current values and identities.</param>
    /// <param name="write">Writes the selected member and returns complete replacement storage.</param>
    /// <param name="paths">Reads identities without accessing the selected leaf.</param>
    public ValidationLens(
        Func<TStorage, ValidationRead<TValue>> read,
        Func<TStorage, TValue, TStorage> write,
        Func<TStorage, IReadOnlyList<ValidationPath>> paths)
        : this(read, write)
    {
        ArgumentExceptionHelper.ThrowIfNull(paths);
        _paths = paths;
    }

    /// <summary>Creates a selector that reads current cell storage and follows replacement.</summary>
    /// <returns>The typed cell selector.</returns>
    public ValidationSelector<ValidationCell<TStorage>, TValue> Selector() =>
        new(cell => _paths is { } paths
            ? new(() => _read(cell.Value), [ValidationDependency.PropertyChanged(() => cell, nameof(cell.Value))], ValidationObservationOptions<TValue>.Default, () => paths(cell.Value))
            : new(() => _read(cell.Value), [ValidationDependency.PropertyChanged(() => cell, nameof(cell.Value))], ValidationObservationOptions<TValue>.Default));

    /// <summary>Creates a target that writes current storage through the complete typed lens.</summary>
    /// <returns>The typed write-back target.</returns>
    /// <remarks>The declared read callback may access the selected leaf and determines availability and slot metadata.
    /// A lens without exactly one path owns the whole storage slot. Peer writes are coalesced and serialized on current storage.
    /// Replacement construction must not directly mutate storage; a direct mutation raises a conflict before stale copy-back.
    /// Reentrant external changes during final commit remain external epochs.</remarks>
    public ValidationTarget<ValidationCell<TStorage>, TValue> Target() => new(cell => new(() =>
    {
        var receipt = cell.StoragePolicy.CreateReceipt();
        var changes = ValidationDependency.PropertyChanged(() => cell, nameof(cell.Value));
        return new(() => Resolve(cell, receipt), [changes, ValidationDependency.Writer(receipt)]);
    }));

    /// <summary>Resolves storage availability and the current structural lens slot.</summary>
    /// <param name="cell">The stable storage owner.</param>
    /// <param name="receipt">The complete storage written by this bound target.</param>
    /// <returns>The current typed assignment or a missing target.</returns>
    private ValidationTargetAccess<TValue> Resolve(ValidationCell<TStorage> cell, ValidationWriteReceipt receipt)
    {
        var read = _read(cell.Value);
        if (!read.HasOwner)
        {
            return ValidationTargetAccess<TValue>.Missing();
        }

        Action<TValue> assign = value => cell.StoragePolicy.Execute(receipt.Origin, () =>
        {
            var revision = cell.Revision;
            var current = _read(cell.Value);
            if (receipt.Origin.IsDisposed || !current.HasOwner)
            {
                return;
            }

            var replacement = _write(cell.Value, value);
            if (receipt.Origin.IsDisposed)
            {
                return;
            }

            if (cell.Revision != revision)
            {
                throw new InvalidOperationException("Storage changed externally during replacement construction. Use a pure typed transformation and the explicit peer write scope.");
            }

            receipt.CaptureSlot(current.Paths.Count == 1 ? current.Paths[0] : null);
            cell.Set(replacement, receipt.Origin);
        });
        Func<TValue, bool> isCurrent = _ => receipt.IsCurrent();
        return read.Paths.Count == 1
            ? ValidationTargetAccess<TValue>.Present(cell, read.Paths[0], assign, isCurrent)
            : ValidationTargetAccess<TValue>.Present(cell, assign, isCurrent);
    }
}
