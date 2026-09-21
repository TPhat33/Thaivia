"""Structural separation of source tags vs visual/simulation assumptions.

This is a *structural* guarantee, not a naming convention: `SourceTags`
and `Assumption` are different, frozen dataclasses. There is no shared
mutable dict that both write into, so an assumption value cannot leak
into the source-tag namespace by accident. `tests/test_pipeline_assumptions.py`
asserts this end to end, including through MapPack serialization.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass(frozen=True)
class SourceTags:
    """Tags exactly as read from the OSM source. Never mutated with
    inferred/assumed values; a missing key means the source did not say,
    which callers must treat as `unknown`, never as a fact."""

    tags: dict[str, str] = field(default_factory=dict)

    def get(self, key: str, default: str | None = None) -> str | None:
        return self.tags.get(key, default)

    def has(self, key: str) -> bool:
        return key in self.tags

    def to_json(self) -> dict[str, str]:
        return dict(sorted(self.tags.items()))


VISUAL_ASSUMPTION = "visual_assumption"
SIMULATION_ASSUMPTION = "simulation_assumption"


@dataclass(frozen=True)
class Assumption:
    """A single value the renderer or simulation needs but the source did
    not provide, plus the rule that produced it. Lives in its own
    namespace (`visual_assumption` or `simulation_assumption`), never
    merged into `SourceTags`."""

    field: str
    value: Any
    rule: str
    namespace: str

    def to_json(self) -> dict[str, Any]:
        return {"field": self.field, "value": self.value, "rule": self.rule}


def make_visual_assumption(field_name: str, value: Any, rule: str) -> Assumption:
    return Assumption(field=field_name, value=value, rule=rule, namespace=VISUAL_ASSUMPTION)


def make_simulation_assumption(field_name: str, value: Any, rule: str) -> Assumption:
    return Assumption(field=field_name, value=value, rule=rule, namespace=SIMULATION_ASSUMPTION)


def split_assumptions(assumptions: list[Assumption]) -> tuple[list[Assumption], list[Assumption]]:
    """Returns (visual, simulation) assumption lists."""
    visual = [a for a in assumptions if a.namespace == VISUAL_ASSUMPTION]
    simulation = [a for a in assumptions if a.namespace == SIMULATION_ASSUMPTION]
    return visual, simulation
