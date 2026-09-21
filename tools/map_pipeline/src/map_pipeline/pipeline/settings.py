"""Importer/settings versioning and the effective-settings hash.

The source lock and MapPack manifest both need to say, honestly,
"this was produced by importer version X against settings hash Y" so
that a settings change (e.g. a different context_buffer_m, a different
candidate_projected_crs) is never silently absorbed into an existing
lock or pack -- it must produce a new one.
"""

from __future__ import annotations

import hashlib
import json
from typing import Any

# Bumped whenever the parsing/projection/normalization/graph/bake logic
# changes in a way that could change output for the same source bytes.
IMPORTER_VERSION = "0.1.0"

# Bumped whenever the MapPack schema's shape changes in a
# backward-incompatible way.
SCHEMA_VERSION = "0.1.0"


def effective_settings(pilot_area_config: dict[str, Any]) -> dict[str, Any]:
    """The subset of config that actually influences pipeline output.

    Deliberately narrow: unrelated config fields (comments, budget knobs
    that only affect *acquisition*, not derivation) do not force a new
    lock/pack when they change.
    """
    return {
        "map_id": pilot_area_config["map_id"],
        "editable_bbox_wgs84_lonlat": list(pilot_area_config["editable_bbox_wgs84_lonlat"]),
        "context_buffer_m": pilot_area_config["context_buffer_m"],
        "candidate_projected_crs": pilot_area_config["candidate_projected_crs"],
        "driving_side": pilot_area_config["driving_side"],
        "importer_version": IMPORTER_VERSION,
        "schema_version": SCHEMA_VERSION,
    }


def settings_hash(pilot_area_config: dict[str, Any]) -> str:
    payload = json.dumps(
        effective_settings(pilot_area_config), sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()
