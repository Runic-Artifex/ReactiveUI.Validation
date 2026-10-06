# Default context and missing ownership source delta

The read-only audit of final runtime topic
`0f84c7490dfdaa7ca7cabcf09b25275cff36490a` requires no new RUC/RDC boundary.
Its six changed shared core files add no reflected member access, expression
execution, runtime activation/generic construction, trim/AOT suppression or
reflection root. The [manifest](evidence/2026-10-06-defaults-ownership-source-delta.json)
pins those files, seven unchanged reachable core files and both complete topic
baselines by Git blob and SHA256. Exact source copies are retained under the
audit worktree's `artifacts/aot-surface-audit/defaults-ownership-source`.

This extends the [merged runtime checkpoint](RuntimeSourceDelta.md). The topic
delta is measured from runtime `140ab94bd435cc8b2ef031c0bbe06136ecb1f8c0`;
its product files are identical to ownership revision
`b3a72dba5a33f76c0f7f29ce8c053bd3a3ff33b9`. The topic independently contains
424 explicit public/protected declarations per flavor, up from 422. Its paired
baselines normalize identically and retain 32 RUC declarations and zero RDC
declarations. This metric excludes enum fields: the added public surface is two
`DefaultContext` overloads and `ValidationPlanRole.DefaultContext = 6`.
The topic does not contain the root's adapter/flag union, so these counts must
not replace the prior merged integration's 446-declaration count.

| Reachable route | Inspected operation and boundary |
| --- | --- |
| `DefaultContext<TModel>()` and `(object? owner)` | Preserve the original `class, IValidatableViewModel` slot. Dispatch checks model provider, model attachment, owner provider and owner attachment using exact closed selector types and a null expression. A provider's success-null result throws. The fallback stores a direct `model.ValidationContext` read and attaches known INPC notifications when available; non-INPC models use an explicit snapshot. |
| Null expression registration | The registry admits null only for `Model` and `DefaultContext`. Exact closed generic entries compare role and expression reference identity; typed schema matching and ambiguity checks remain unchanged. There is no expression evaluation or member discovery in lookup. |
| `WithComparer` | Copies policy and typed dependency contracts. Cold plans wrap the supplied fresh-plan factory and retain its policy checks. Construction and comparison adaptation do not invoke the value getter or metadata reader. |
| Ownership frames and switching | `ValidationOwnerRead<TValue>` carries actual owner presence, selected value and suppressed absence. Each ownership facade turns suppressed absence into an empty inner stream before `Switch`, detaching the previous model/helper/context/state/rule stream. Present-null remains an ordinary clearing selection. Synthetic fallback values are excluded from known owner roles. |
| Ordinary selected values | `SelectedOwnerComparer<TValue>` checks only fixed closed `ValidationHelper`, `IValidationContext` and `IObservable<IValidationState>` assignability. Reference identity applies to these known owner contracts. Ordinary values, including `object`-typed selections, retain the supplied value comparer and policy-adjusted fallback passed to the caller's typed state callback. |
| Normal context binding | The two context facades call typed `ObserveModelContextState`; ownership policy survives to inner stream selection. The target and formatter paths retain the previously reviewed typed contracts. |

The new `typeof(TValue).IsValueType`/`IsAssignableFrom` checks classify closed
type metadata; they do not enumerate members, construct unknown generic types
or access selected values. The existing `typeof(TView).IsValueType` check still
rejects an unsupported model fallback. The added `SST2307` suppressions explain
non-inferable model type parameters and suppress no IL warning.

Getter inspection finds no added reevaluation: provider lookup invokes only
the supplied typed provider/matcher/factory; `WithComparer` retains read
delegates; ownership projection consumes one delivered read frame. The default
fallback reads `ValidationContext` only inside its snapshot delegate. Exact
evaluation counts and subscription behavior still require execution evidence;
this source report does not substitute for the runtime owner's focused tests.

No new dependency operation extends the previously inspected typed
Defer/Select/CombineLatest/DistinctUntilChanged/Return/Empty/Switch and
notification routes. No dependency bytes were newly inspected here. The 32
existing public RUC boundaries remain the real expression observation and
assignment operations described by earlier checkpoints; none is introduced
into these typed routes.

No build or execution was launched for this delta. The root's final runtime,
adapter and net10-only `IsAotCompatible` union must be pinned together before
fresh strict producer/packed byte inspection and actual packed managed,
full-trim and native Linux/Windows complete-corpus gates close the shipping
decision. Precise Unsafe negative diagnostics remain part of that gate.
