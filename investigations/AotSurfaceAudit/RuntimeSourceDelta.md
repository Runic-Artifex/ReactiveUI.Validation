# Runtime ownership source delta

The read-only source audit of merged runtime
`08ed1c616746e09e3bcacde76eadf555aa50b3c8` adds no RUC/RDC boundary and no
trim/AOT suppression or reflection root. The
[delta manifest](evidence/2026-10-06-runtime-source-delta.json) pins all ten
changed/new shared core source files and both complete baselines. Each baseline
now contains 446 explicit public/protected declarations, 32 RUC declarations
and zero RDC declarations; the complete paired baselines normalize identically
under the declared namespace/scheduler/friend-assembly differences.

This extends the [422-declaration source checkpoint](IntegratedSourceCheckpoint.md).
It is not a final shipping decision: the DefaultContext helper/null-role
followup and the fresh flagged producer/packed managed/full-trim/native gates
remain pending. No build or execution was launched for this source delta.

| Added or changed route | Actual source operation |
| --- | --- |
| `ValidationStoragePolicy`, `ValidationWriteOrigin`, `ValidationWriteReceipt` | Explicit typed transforms/commits, origin reference identity, revision counters, finite queue/dictionary state and typed failure callbacks. No member inference or reflected copy-back. |
| Scope failure propagation | `Drain` invokes the admitted `Action`; `FailScope` explicitly invokes failed/discarded origins' failure delegates before propagating the original failure. `Writer` attaches the receipt's typed observer and cleanup. |
| Cell/lens writes | Cell storage records the explicit policy before notifications. Each bound subscription creates its own receipt and delegates; lens copy-back reads current typed storage and verifies its revision before committing. |
| Cold access/write factories | Supplied `Func<ValidationAccessPlan<TValue>>` or `Func<ValidationWritePlan<TOut>>` factories create concrete typed plans per observation/binding. Null/self/policy errors are explicit; there is no activation or generic construction through reflection. |
| `AfterRead` dependencies | Ordinary typed owners attach first, then the typed snapshot is read, then cached branch owners attach before that snapshot is delivered. The owner getter contract forbids reevaluating the selected expression. Metadata observations still require a separate metadata-only reader. |

The additional reachable dependency operation is `Signal.Defer` in pinned
ReactiveUI.Primitives `9.0.0`, source
`56523df08d686aba128e0abef660a855fd7951f3`. Its concrete
`DeferSignal<T>` invokes a supplied typed factory and subscribes through typed
`SignalSubscription`/`GuardedWitness<T>` helpers. The selected source hashes and
primary links are in the manifest. Factory failure becomes a typed failure
observable. There is no expression compilation, reflected value access,
implementation scanning or runtime generic construction on this route.

The 32 existing public RUC boundaries remain those of the retained expression
constructors and explicit Unsafe observation/assignment APIs. The new ownership
and cold-factory routes do not reach them. Final actual producer/package IL must
confirm the completed source and its dependency cohort; earlier cached-byte
inspection does not close that gate.
