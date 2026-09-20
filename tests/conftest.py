from __future__ import annotations

from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parent.parent


@pytest.fixture(scope="session")
def repo_root() -> Path:
    return REPO_ROOT


@pytest.fixture(scope="session")
def configs_dir(repo_root: Path) -> Path:
    return repo_root / "configs"


@pytest.fixture(scope="session")
def schemas_dir(repo_root: Path) -> Path:
    return repo_root / "tools" / "map_pipeline" / "schemas"


@pytest.fixture(scope="session")
def synthetic_fixtures_dir(repo_root: Path) -> Path:
    return repo_root / "tests" / "fixtures" / "synthetic"
