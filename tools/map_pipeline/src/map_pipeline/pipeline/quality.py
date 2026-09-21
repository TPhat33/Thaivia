"""The quality/audit report: unknown counts, dropped/quarantined feature
IDs with reasons, incomplete relations, unsupported restrictions,
topology conflicts, measured geometry round-trip error, and retained vs
rejected counts.

Nothing in this module invents a number. Every count is derived from
what the rest of the pipeline actually observed; when there is no real
source yet, callers must keep reporting `blocked`/`not_measured` (see
`commands/audit.py`), never zero-fill this report and call it real.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass, field


@dataclass(frozen=True)
class QualityIssue:
    kind: str
    feature_id: str
    detail: str

    def to_json(self) -> dict[str, str]:
        return {"kind": self.kind, "feature_id": self.feature_id, "detail": self.detail}


@dataclass
class QualityReport:
    unknown_counts: dict[str, int] = field(default_factory=dict)
    dropped: list[QualityIssue] = field(default_factory=list)
    quarantined: list[QualityIssue] = field(default_factory=list)
    incomplete_relations: list[QualityIssue] = field(default_factory=list)
    unsupported_restrictions: list[QualityIssue] = field(default_factory=list)
    topology_conflicts: list[QualityIssue] = field(default_factory=list)
    duplicate_associations: list[QualityIssue] = field(default_factory=list)
    retained_counts: dict[str, int] = field(default_factory=dict)
    rejected_counts: dict[str, int] = field(default_factory=dict)
    roundtrip_error_m: dict[str, float | int] = field(
        default_factory=lambda: {"max": 0.0, "mean": 0.0, "sample_count": 0}
    )

    def add(self, issue: QualityIssue) -> None:
        bucket = {
            "dropped_feature": self.dropped,
            "quarantined_feature": self.quarantined,
            "incomplete_relation": self.incomplete_relations,
            "unsupported_restriction": self.unsupported_restrictions,
            "topology_conflict": self.topology_conflicts,
            "duplicate_poi_building_association": self.duplicate_associations,
        }.get(issue.kind)
        if bucket is None:
            # Unknown issue kinds still must not be lost silently.
            self.dropped.append(issue)
        else:
            bucket.append(issue)

    def to_json(self) -> dict:
        return {
            "unknown_counts": dict(sorted(self.unknown_counts.items())),
            "dropped": [i.to_json() for i in self.dropped],
            "quarantined": [i.to_json() for i in self.quarantined],
            "incomplete_relations": [i.to_json() for i in self.incomplete_relations],
            "unsupported_restrictions": [i.to_json() for i in self.unsupported_restrictions],
            "topology_conflicts": [i.to_json() for i in self.topology_conflicts],
            "duplicate_associations": [i.to_json() for i in self.duplicate_associations],
            "retained_counts": dict(sorted(self.retained_counts.items())),
            "rejected_counts": dict(sorted(self.rejected_counts.items())),
            "roundtrip_error_m": dict(self.roundtrip_error_m),
        }
