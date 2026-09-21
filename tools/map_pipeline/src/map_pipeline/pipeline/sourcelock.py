"""Source lock: what `acquire` produces to pin exactly which bytes, from
where, were used to derive a MapPack.

Required fields (spec): source URL or local path, retrieval timestamp,
source snapshot timestamp OR an explicit unknown-reason string, SHA-256
of the exact bytes, file size, importer version, settings version, and a
hash of the effective settings.

Immutability model: each individual lock is content-addressed by
`(sha256-of-bytes, settings_hash)` and, once written to its
content-addressed path, is never overwritten -- a different settings
hash or different source bytes always lands at a *different* filename.
`<map_id>.sourcelock.json` is a convenience pointer to the most recent
lock; it may be refreshed to point at a newer lock, but no existing
versioned lock file is ever mutated or deleted by this module.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from map_pipeline.pipeline.settings import IMPORTER_VERSION, SCHEMA_VERSION, settings_hash

CHUNK_SIZE = 1024 * 1024


def sha256_file(path: Path) -> tuple[str, int]:
    h = hashlib.sha256()
    size = 0
    with path.open("rb") as fh:
        while True:
            chunk = fh.read(CHUNK_SIZE)
            if not chunk:
                break
            h.update(chunk)
            size += len(chunk)
    return h.hexdigest(), size


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


@dataclass(frozen=True)
class SourceLock:
    map_id: str
    source_kind: str  # "url" | "local_file"
    source_location: str  # the URL, or the local path as given
    retrieved_at: str  # ISO 8601 UTC
    source_snapshot_timestamp: str | None
    source_snapshot_unknown_reason: str | None
    sha256: str
    size_bytes: int
    importer_version: str
    schema_version: str
    settings_version: str
    settings_hash: str

    def to_json(self) -> dict[str, Any]:
        return {
            "map_id": self.map_id,
            "source_kind": self.source_kind,
            "source_location": self.source_location,
            "retrieved_at": self.retrieved_at,
            "source_snapshot_timestamp": self.source_snapshot_timestamp,
            "source_snapshot_unknown_reason": self.source_snapshot_unknown_reason,
            "sha256": self.sha256,
            "size_bytes": self.size_bytes,
            "importer_version": self.importer_version,
            "schema_version": self.schema_version,
            "settings_version": self.settings_version,
            "settings_hash": self.settings_hash,
        }

    @staticmethod
    def from_json(data: dict[str, Any]) -> "SourceLock":
        return SourceLock(
            map_id=data["map_id"],
            source_kind=data["source_kind"],
            source_location=data["source_location"],
            retrieved_at=data["retrieved_at"],
            source_snapshot_timestamp=data.get("source_snapshot_timestamp"),
            source_snapshot_unknown_reason=data.get("source_snapshot_unknown_reason"),
            sha256=data["sha256"],
            size_bytes=data["size_bytes"],
            importer_version=data["importer_version"],
            schema_version=data["schema_version"],
            settings_version=data["settings_version"],
            settings_hash=data["settings_hash"],
        )


def build_source_lock(
    *,
    map_id: str,
    source_kind: str,
    source_location: str,
    sha256: str,
    size_bytes: int,
    pilot_area_config: dict[str, Any],
    source_snapshot_timestamp: str | None = None,
    source_snapshot_unknown_reason: str | None = None,
    now: datetime | None = None,
) -> SourceLock:
    if source_snapshot_timestamp is None and source_snapshot_unknown_reason is None:
        source_snapshot_unknown_reason = (
            "source snapshot time not derivable from this file/transfer; "
            "OSM PBF/XML extracts do not always carry a reliable snapshot timestamp header"
        )
    now = now or datetime.now(timezone.utc)
    return SourceLock(
        map_id=map_id,
        source_kind=source_kind,
        source_location=source_location,
        retrieved_at=now.isoformat(),
        source_snapshot_timestamp=source_snapshot_timestamp,
        source_snapshot_unknown_reason=source_snapshot_unknown_reason,
        sha256=sha256,
        size_bytes=size_bytes,
        importer_version=IMPORTER_VERSION,
        schema_version=SCHEMA_VERSION,
        settings_version=SCHEMA_VERSION,
        settings_hash=settings_hash(pilot_area_config),
    )


def versioned_lock_filename(map_id: str, sha256_hex: str, settings_hash_hex: str) -> str:
    return f"{map_id}.{sha256_hex[:16]}.{settings_hash_hex[:12]}.sourcelock.json"


def canonical_lock_filename(map_id: str) -> str:
    return f"{map_id}.sourcelock.json"


def guess_source_extension(location: str) -> str:
    """osmium detects file format from the filename extension, so the
    content-addressed cache filename must keep a recognizable one (it
    cannot rely on the original path's directory/basename, since two
    different source locations can hash to the same hex prefix territory
    only in astronomically unlikely cases, but must always keep a valid
    extension). Recognizes the extensions osmium/Geofabrik/Overpass
    actually use; anything else falls back to `.osm.xml` since that is
    the most common bounded-extract format and osmium can sniff plain
    XML content even with a slightly-wrong-but-still-`.xml`-shaped name."""
    lowered = location.lower()
    for ext in (".osm.pbf", ".osm.bz2", ".osm.gz", ".osm.xml", ".pbf", ".xml"):
        if lowered.endswith(ext):
            return ext
    return ".osm.xml"


def source_bytes_filename(map_id: str, sha256_hex: str, source_location: str = "") -> str:
    """Content-addressed filename for the raw source bytes a lock pins,
    so `build`/`verify` can always find the exact bytes a given lock
    refers to regardless of how many times `acquire` has since been
    re-run with different settings or a different source. Keeps a real
    file extension (guessed from `source_location` when given) because
    osmium picks its parser by filename suffix."""
    ext = guess_source_extension(source_location) if source_location else ".osm.xml"
    return f"{map_id}.{sha256_hex[:16]}.source{ext}"


def write_source_lock(cache_dir: Path, lock: SourceLock) -> dict[str, Any]:
    """Writes the content-addressed (immutable) lock file if it does not
    already exist, and refreshes the canonical pointer file to it. No
    existing versioned lock file is ever overwritten -- a different
    sha256 or settings_hash always produces a new filename."""
    cache_dir.mkdir(parents=True, exist_ok=True)
    versioned_path = cache_dir / versioned_lock_filename(lock.map_id, lock.sha256, lock.settings_hash)
    payload = json.dumps(lock.to_json(), indent=2, sort_keys=True) + "\n"
    is_new = not versioned_path.exists()
    if is_new:
        versioned_path.write_text(payload, encoding="utf-8")
    canonical_path = cache_dir / canonical_lock_filename(lock.map_id)
    canonical_path.write_text(payload, encoding="utf-8")
    return {
        "versioned_path": versioned_path,
        "canonical_path": canonical_path,
        "is_new_lock": is_new,
    }


def read_source_lock(path: Path) -> SourceLock:
    data = json.loads(path.read_text(encoding="utf-8"))
    return SourceLock.from_json(data)
