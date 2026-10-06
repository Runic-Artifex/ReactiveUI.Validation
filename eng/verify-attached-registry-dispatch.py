#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Check framework-only InspectIl output without loading or executing product assemblies.

Feed output from eng/tools/InspectIl for both core DLLs.
This checks the three built-in attachment sites, while retaining each explicit
custom-provider interface branch. It is a dispatch-shape check, not crash proof.
"""

import argparse
import json
from pathlib import Path
import re

NAMESPACES = (
    "ReactiveUI.Validation.Capabilities",
    "ReactiveUI.Validation.Reactive.Capabilities",
)
SITES = {
    "TryResolveSelectorOn": "TryGetSelector",
    "TryResolveTargetCore": "TryGetTarget",
    "ObserveModelReads": "TryGetSelector",
}
CALL = re.compile(r"^ IL_[0-9a-f]+ (call|callvirt) (.+)$")


def verify(text):
    """Require concrete generic registry calls and unchanged custom-provider branches."""
    sections = []
    current = None
    method = None
    for line in text.splitlines():
        if line.startswith("ASSEMBLY "):
            current = {"identity": line, "methods": {}}
            sections.append(current)
            method = None
        elif line.startswith("METHOD ") and current is not None:
            method = line.removeprefix("METHOD ")
            current["methods"].setdefault(method, [])
        elif current is not None and method is not None:
            instruction = CALL.fullmatch(line)
            if instruction:
                current["methods"][method].append(instruction.groups())

    results = []
    for namespace in NAMESPACES:
        runtime = f"{namespace}.ValidationRuntime."
        matching = [section for section in sections if any(name.startswith(runtime) for name in section["methods"])]
        if len(matching) != 1:
            raise ValueError(f"Expected exactly one inspected core assembly for {namespace}.")
        section = matching[0]
        for site, operation in SITES.items():
            bodies = [calls for name, calls in section["methods"].items() if name.startswith(f"{runtime}{site}(")]
            if len(bodies) != 1:
                raise ValueError(f"Expected exactly one {runtime}{site} body.")
            calls = bodies[0]
            registry = [target for _, target in calls if target.startswith(f"{namespace}.ValidationPlanRegistry.{operation}")]
            provider = [target for _, target in calls if target.startswith(f"{namespace}.IValidationPlanProvider.{operation}")]
            lookup = [target for _, target in calls if target.startswith(f"{namespace}.ValidationPlanRegistry.TryGetAttached(")]
            if len(registry) != 1 or "<!!" not in registry[0]:
                raise ValueError(f"{runtime}{site} must call exactly one concrete registry generic MethodSpec.")
            if len(provider) != 1 or "<!!" not in provider[0]:
                raise ValueError(f"{runtime}{site} must retain exactly one custom-provider interface MethodSpec.")
            if len(lookup) != 1:
                raise ValueError(f"{runtime}{site} must retain exactly one attached-registry lookup.")
            results.append({"assembly": section["identity"], "site": site, "registry_call": registry[0], "custom_provider_call": provider[0]})
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("il_output", nargs="+", type=Path)
    args = parser.parse_args()
    print(json.dumps({"scope": "Built-in concrete attachment dispatch; custom provider interface dispatch retained", "sites": verify("\n".join(path.read_text() for path in args.il_output))}, indent=2))


if __name__ == "__main__":
    main()
