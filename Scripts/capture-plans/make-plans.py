#!/usr/bin/env python3
"""Regenerate the standard contemporary-shell capture plans."""

import json
import os


HERE = os.path.dirname(os.path.abspath(__file__))
PROFILES = ["Cellar", "TastingRoom"]
SIZES = [(1456, 1060), (960, 720)]
NARROW = (720, 560)
ROUTES = [
    "Overview",
    "Library",
    "Downloads",
    "Processing",
    "History",
    "Accounts",
    "Settings",
    "Tools",
    "Trash",
    "About",
]


def entries(routes, sizes):
    return [
        {"profile": profile, "route": route, "width": width, "height": height}
        for profile in PROFILES
        for route in routes
        for width, height in sizes
    ]


def s7_variant_entries():
    routes = ["Overview", "Library", "Processing"]
    variants = [
        ("compact", {"density": "Compact"}),
        ("decoration-off", {"decoration": "Off"}),
        ("reduced-motion", {"motion": "Reduce"}),
        ("scale-200", {"logicalScale": 2}),
    ]
    result = []
    for variant_name, settings in variants:
        for profile in PROFILES:
            for route in routes:
                entry = {
                    "profile": profile,
                    "route": route,
                    "width": 1456,
                    "height": 1060,
                    "file": f"{variant_name}-{profile.lower()}-{route.lower()}.png",
                    **settings,
                }
                if route == "Processing":
                    entry["processingScenario"] = "Mixed"
                result.append(entry)

    for route in routes:
        entry = {
            "profile": "HighContrast",
            "route": route,
            "width": 1456,
            "height": 1060,
            "file": f"high-contrast-{route.lower()}.png",
        }
        if route == "Processing":
            entry["processingScenario"] = "Mixed"
        result.append(entry)
    return result


PLANS = {
    "all-routes.json": entries(ROUTES, SIZES + [NARROW]),
    "s2-shell.json": entries(ROUTES, SIZES + [NARROW]),
    "overview.json": entries(["Overview"], SIZES),
    "library.json": entries(["Library"], SIZES),
    "processing.json": entries(["Processing"], SIZES),
    "secondary.json": entries(
        ["Downloads", "History", "Accounts", "Settings", "Tools", "Trash"], SIZES
    )
    + [
        {
            "profile": "Cellar",
            "surface": "Onboarding",
            "onboardingStep": step,
            "onboardingScanActive": step == 4,
            "width": 1456,
            "height": 1060,
        }
        for step in range(1, 6)
    ],
    "s7-variant-matrix.json": s7_variant_entries(),
}


for name, items in PLANS.items():
    with open(os.path.join(HERE, name), "w", encoding="utf-8") as plan_file:
        json.dump({"settleMs": 900, "entries": items}, plan_file, indent=2)
        plan_file.write("\n")
    print(f"{name}: {len(items)} entries")
