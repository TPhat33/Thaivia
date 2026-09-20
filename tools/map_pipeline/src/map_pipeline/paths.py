"""Repo-root and path resolution shared by every subcommand.

This tool is designed to run inside a checkout of the Thaivia repo (it is
not published as a standalone distribution). Paths are resolved, in
order:
1. the ``THAIVIA_REPO_ROOT`` environment variable, if set;
2. a walk up from this source file's own location, which works for an
   editable (``pip install -e``) install inside the checkout;
3. the current working directory, as a last resort.
"""

from __future__ import annotations

import os
from pathlib import Path


def _looks_like_repo_root(path: Path) -> bool:
    return (path / "configs").is_dir() and (path / "tools" / "map_pipeline").is_dir()


def resolve_repo_root() -> Path:
    env_root = os.environ.get("THAIVIA_REPO_ROOT")
    if env_root:
        candidate = Path(env_root).resolve()
        if _looks_like_repo_root(candidate):
            return candidate

    # tools/map_pipeline/src/map_pipeline/paths.py -> repo root is 4
    # parents up from this file's directory.
    here = Path(__file__).resolve()
    candidate = here.parents[4] if len(here.parents) >= 4 else here.parent
    if _looks_like_repo_root(candidate):
        return candidate

    cwd = Path.cwd()
    if _looks_like_repo_root(cwd):
        return cwd

    # Nothing definitive found; return cwd and let callers report missing
    # files explicitly rather than silently guessing.
    return cwd


def repo_root() -> Path:
    return resolve_repo_root()


def configs_dir() -> Path:
    return repo_root() / "configs"


def schemas_dir() -> Path:
    return Path(__file__).resolve().parents[2] / "schemas"


def cache_dir() -> Path:
    return repo_root() / "data" / "cache"


def evidence_dir() -> Path:
    return repo_root() / "docs" / "evidence"
