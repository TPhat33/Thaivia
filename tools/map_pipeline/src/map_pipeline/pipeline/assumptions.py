"""Concrete visual/simulation assumption rules.

Every rule here only fires when the source tag is genuinely absent; it
never overrides a real source value. Each produced `Assumption` carries
the rule name that produced it so it is inspectable/correctable later
(per spec: "values the renderer or game needs but the source lacks ...
must be stored in separate namespaced fields ... with the rule that
produced them").
"""

from __future__ import annotations

from map_pipeline.pipeline.tags import Assumption, SourceTags, make_simulation_assumption, make_visual_assumption

DEFAULT_BUILDING_HEIGHT_M = 6.0
METRES_PER_LEVEL = 3.2

DEFAULT_ROAD_WIDTH_M_BY_CLASS = {
    "motorway": 14.0,
    "trunk": 12.0,
    "primary": 10.0,
    "secondary": 9.0,
    "tertiary": 8.0,
    "residential": 6.0,
    "service": 4.0,
    "footway": 2.0,
    "path": 1.5,
    "track": 3.0,
}
DEFAULT_ROAD_WIDTH_FALLBACK_M = 5.0

DEFAULT_MAXSPEED_KPH_BY_CLASS = {
    "motorway": 100,
    "trunk": 80,
    "primary": 60,
    "secondary": 50,
    "tertiary": 50,
    "residential": 30,
    "service": 20,
    "footway": 5,
    "path": 5,
    "track": 20,
}
DEFAULT_MAXSPEED_FALLBACK_KPH = 30


def building_assumptions(tags: SourceTags) -> list[Assumption]:
    assumptions: list[Assumption] = []
    if tags.has("height"):
        return assumptions  # real source fact; no assumption needed
    levels_raw = tags.get("building:levels")
    if levels_raw is not None:
        try:
            levels = float(levels_raw)
            assumptions.append(
                make_visual_assumption(
                    "height_m",
                    round(levels * METRES_PER_LEVEL, 2),
                    f"height_from_building_levels_x{METRES_PER_LEVEL}m_v1",
                )
            )
            return assumptions
        except ValueError:
            pass
    assumptions.append(
        make_visual_assumption(
            "height_m",
            DEFAULT_BUILDING_HEIGHT_M,
            "default_building_height_no_levels_or_height_tag_v1",
        )
    )
    return assumptions


def road_assumptions(tags: SourceTags) -> list[Assumption]:
    assumptions: list[Assumption] = []
    highway = tags.get("highway", "unclassified")

    if not tags.has("width") and not tags.has("lanes"):
        width = DEFAULT_ROAD_WIDTH_M_BY_CLASS.get(highway, DEFAULT_ROAD_WIDTH_FALLBACK_M)
        assumptions.append(
            make_visual_assumption(
                "width_m",
                width,
                f"default_road_width_by_highway_class_v1:{highway}",
            )
        )

    if not tags.has("maxspeed"):
        speed = DEFAULT_MAXSPEED_KPH_BY_CLASS.get(highway, DEFAULT_MAXSPEED_FALLBACK_KPH)
        assumptions.append(
            make_simulation_assumption(
                "maxspeed_kph",
                speed,
                f"default_maxspeed_by_highway_class_v1:{highway}",
            )
        )
    return assumptions
