"""Quantization for the serialized payload, kept structurally separate
from canonical source-precision geometry.

Canonical geometry (the pipeline's internal float64 local-meter
coordinates) is never mutated by quantization or by any render
simplification. Both `render_1cm` (quantized) and, if present, a
`render_simplified` derived field are computed FROM canonical geometry
and stored alongside it, never replacing it.
"""

from __future__ import annotations

Coord = tuple[float, float]

QUANTUM_M = 0.01  # 1 cm


def quantize_1cm(value_m: float) -> float:
    return round(value_m / QUANTUM_M) * QUANTUM_M


def quantize_coords_1cm(coords: list[Coord]) -> list[Coord]:
    return [(quantize_1cm(x), quantize_1cm(z)) for x, z in coords]


def canonical_round(value_m: float) -> float:
    """Canonical geometry keeps far more precision than the 1 cm render
    quantum (sub-micron), purely to collapse float64 formatting noise for
    byte-deterministic serialization -- this is NOT the quantization step
    and must never be confused with it in the quality report."""
    return round(value_m, 9)


def canonical_round_coords(coords: list[Coord]) -> list[Coord]:
    return [(canonical_round(x), canonical_round(z)) for x, z in coords]
