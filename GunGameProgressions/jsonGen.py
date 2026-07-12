import argparse
import json
from pathlib import Path


ENEMY_TYPES = [
    "RW_Rot",
]


def parse_args():
    parser = argparse.ArgumentParser(description="Generate advanced vanilla GunGame pools")
    parser.add_argument("sosig_type", type=int, choices=range(len(ENEMY_TYPES)))
    parser.add_argument("--input", type=Path, default=Path("ObjectData.json"), help="GunGame metadata JSON")
    parser.add_argument("--output-dir", type=Path, default=Path("."), help="Directory for generated pool JSON")
    parser.add_argument(
        "--rules",
        type=Path,
        default=Path(__file__).with_name("profile-rules.json"),
        help="Shared GunGame profile rules JSON",
    )
    parser.add_argument("--seed", type=int, default=0, help="Retained for backward-compatible automation")
    parser.add_argument(
        "--source-profile",
        type=Path,
        help="Existing legacy or Advanced profile to convert while preserving its selected guns and metadata",
    )
    parser.add_argument(
        "--output-name",
        help="Output filename for --source-profile conversion; defaults to the selected faction profile",
    )
    parser.add_argument("--profile-name", help="Explicit advanced profile name")
    parser.add_argument("--description", help="Explicit advanced profile description")
    parser.add_argument(
        "--enemy-types",
        help="Comma-separated enemy identifiers for an advanced profile",
    )
    parser.add_argument(
        "--enemy-values",
        help="Comma-separated Points or Count values matching --enemy-types",
    )
    parser.add_argument(
        "--enemy-progression-type",
        type=int,
        choices=(0, 1, 2),
        help="GunGame Count (0), Points (1), or Tiers (2) progression mode",
    )
    parser.add_argument(
        "--order-type",
        type=int,
        choices=(0, 1, 2),
        help="GunGame fixed (0), random (1), or random-within-category (2) weapon order",
    )
    return parser.parse_args()


def load_rules(path):
    rules = json.loads(path.read_text(encoding="utf-8-sig"))
    return {
        "firearms": set(rules.get("firearmBlacklist", [])),
        "feeds": set(rules.get("feedBlacklist", [])),
        "offline_primary_feeds": rules.get("offlinePrimaryFeeds", {}),
    }


def vanilla_items(items, category, blacklist):
    return [
        item
        for item in items
        if item.get("Category") == category
        and not item.get("IsModContent", False)
        and item.get("ObjectID") not in blacklist
    ]


def load_pool(path):
    if path is None or not path.exists():
        return None

    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return None


def load_preserved_assignments(existing_pool, available_vanilla_ids):
    if existing_pool is None:
        return {}

    assignments = {}
    if existing_pool.get("WeaponPoolType") == "Advanced":
        for gun in existing_pool.get("Guns", []):
            gun_name = gun.get("GunName")
            mag_name = gun.get("MagName")
            category_id = gun.get("CategoryID")
            if gun_name in available_vanilla_ids and mag_name in available_vanilla_ids and category_id in (0, 1, 2):
                assignments[gun_name] = (mag_name, category_id)
        return assignments

    for gun_name, mag_name, category_id in zip(
        existing_pool.get("GunNames", []),
        existing_pool.get("MagNames", []),
        existing_pool.get("CategoryIDs", []),
    ):
        if gun_name in available_vanilla_ids and mag_name in available_vanilla_ids and category_id in (0, 1, 2):
            assignments[gun_name] = (mag_name, category_id)
    return assignments


def selected_gun_ids(existing_pool):
    if existing_pool is None:
        return None

    if existing_pool.get("WeaponPoolType") == "Advanced":
        return {gun.get("GunName") for gun in existing_pool.get("Guns", []) if gun.get("GunName")}
    return {object_id for object_id in existing_pool.get("GunNames", []) if object_id}


def profile_enemy_type(existing_pool, fallback):
    if existing_pool is None:
        return fallback

    enemy_type = existing_pool.get("EnemyType")
    if enemy_type:
        return enemy_type

    enemies = existing_pool.get("Enemies", [])
    if enemies:
        return enemies[0].get("EnemyNameString", fallback)
    return fallback


def category_id(item):
    return {
        "Magazine": 0,
        "Clip": 1,
        "SpeedLoader": 2,
        "Cartridge": 2,
    }.get(item.get("Category"))


def direct_feed_candidates(firearm, items_by_id, eligible_feed_ids):
    for key, category in (
        ("CompatibleMagazines", "Magazine"),
        ("CompatibleClips", "Clip"),
        ("CompatibleSpeedLoaders", "SpeedLoader"),
        ("CompatibleSingleRounds", "Cartridge"),
    ):
        direct_ids = firearm.get(key, [])
        if direct_ids:
            candidates = []
            seen = set()
            for object_id in direct_ids:
                item = items_by_id.get(object_id)
                if (
                    item
                    and item.get("Category") == category
                    and object_id in eligible_feed_ids
                    and object_id not in seen
                ):
                    candidates.append(item)
                    seen.add(object_id)
            return candidates
    return None


def fallback_feed_candidates(firearm, magazines, clips, speedloaders, cartridges):
    if firearm.get("MagazineType"):
        magazine_candidates = [item for item in magazines if item.get("MagazineType") == firearm["MagazineType"]]
        if magazine_candidates:
            return magazine_candidates
    if firearm.get("ClipType"):
        clip_candidates = [item for item in clips if item.get("ClipType") == firearm["ClipType"]]
        if clip_candidates:
            return clip_candidates
    if firearm.get("RoundType"):
        speedloader_candidates = [item for item in speedloaders if item.get("RoundType") == firearm["RoundType"]]
        if speedloader_candidates:
            return speedloader_candidates
        return [item for item in cartridges if item.get("RoundType") == firearm["RoundType"]]
    return []


def compatible_feeds(firearm, items_by_id, eligible_feed_ids, magazines, clips, speedloaders, cartridges):
    candidates = direct_feed_candidates(firearm, items_by_id, eligible_feed_ids)
    if candidates is None:
        candidates = fallback_feed_candidates(firearm, magazines, clips, speedloaders, cartridges)
    return sorted({item["ObjectID"]: item for item in candidates}.values(), key=lambda item: item["ObjectID"])


def preferred_feed_category(firearm, feeds):
    if feeds:
        return feeds[0].get("Category")
    if firearm.get("CompatibleMagazines") or firearm.get("MagazineType"):
        return "Magazine"
    if firearm.get("CompatibleClips") or firearm.get("ClipType"):
        return "Clip"
    if firearm.get("CompatibleSpeedLoaders"):
        return "SpeedLoader"
    return "Cartridge"


def compatible_vanilla_scope(firearm, scopes, seed):
    mounts = {
        mount
        for mount in firearm.get("FirearmMounts", [])
        if mount and mount != "Bespoke"
    }
    candidates = [scope for scope in scopes if scope.get("AttachmentMount") in mounts]
    if not candidates:
        return ""

    offset = sum(ord(character) for character in firearm["ObjectID"]) + seed
    return candidates[offset % len(candidates)]["ObjectID"]


def build_pool(
    items,
    enemy_type,
    output_path,
    rules,
    source_pool=None,
    profile_name=None,
    description=None,
    enemy_types=None,
    enemy_values=None,
    enemy_progression_type=None,
    order_type=None,
    seed=0,
):
    available_vanilla_ids = {
        item["ObjectID"] for item in items if not item.get("IsModContent", False) and item.get("ObjectID")
    }
    items_by_id = {item["ObjectID"]: item for item in items if item.get("ObjectID")}
    magazines = vanilla_items(items, "Magazine", rules["feeds"])
    clips = vanilla_items(items, "Clip", rules["feeds"])
    speedloaders = vanilla_items(items, "SpeedLoader", rules["feeds"])
    cartridges = vanilla_items(items, "Cartridge", rules["feeds"])
    eligible_feeds = {
        item["ObjectID"]
        for item in magazines + clips + speedloaders + cartridges
        if item.get("ObjectID")
    }
    vanilla_scopes = sorted(
        [
            item
            for item in vanilla_items(items, "Attachment", set())
            if item.get("AttachmentFeature") == "Magnification"
            and "magnifier" not in item.get("ObjectID", "").lower()
            and item.get("AttachmentMount") not in (None, "", "None", "Bespoke")
        ],
        key=lambda item: item["ObjectID"],
    )
    existing_pool = source_pool if source_pool is not None else load_pool(output_path)
    preserved_assignments = load_preserved_assignments(existing_pool, available_vanilla_ids)
    selected_ids = selected_gun_ids(source_pool)

    guns = []
    seen_ids = set()
    eligible_firearms = vanilla_items(items, "Firearm", rules["firearms"])
    if selected_ids is not None:
        eligible_ids = {item["ObjectID"] for item in eligible_firearms}
        unavailable = sorted(selected_ids - eligible_ids)
        if unavailable:
            raise ValueError("Selected source profile contains unavailable or blacklisted firearms: " + ", ".join(unavailable))
        eligible_firearms = [item for item in eligible_firearms if item["ObjectID"] in selected_ids]

    for firearm in eligible_firearms:
        object_id = firearm["ObjectID"]
        if object_id in seen_ids:
            continue
        seen_ids.add(object_id)

        feeds = compatible_feeds(
            firearm,
            items_by_id,
            eligible_feeds,
            magazines,
            clips,
            speedloaders,
            cartridges,
        )
        primary_id = rules["offline_primary_feeds"].get(object_id)
        primary_category = None
        if object_id in preserved_assignments:
            primary_id = preserved_assignments[object_id][0]

        if primary_id and primary_id in eligible_feeds:
            primary_item = items_by_id[primary_id]
            if primary_item.get("Category") == preferred_feed_category(firearm, feeds):
                feeds = [primary_item] + [item for item in feeds if item["ObjectID"] != primary_id]
                primary_category = category_id(primary_item)

        if not feeds:
            continue

        primary = feeds[0]
        guns.append(
            {
                "GunName": object_id,
                "MagName": primary["ObjectID"],
                "MagNames": [item["ObjectID"] for item in feeds],
                "CategoryID": primary_category if primary_category is not None else category_id(primary),
                "Extra": compatible_vanilla_scope(firearm, vanilla_scopes, seed),
            }
        )

    effective_enemy_types = enemy_types or [profile_enemy_type(source_pool, enemy_type)]
    if enemy_values is None:
        enemy_values = [1] * len(effective_enemy_types)
    if len(enemy_values) != len(effective_enemy_types):
        raise ValueError("--enemy-values must contain one value for each --enemy-types entry")

    return {
        "WeaponPoolType": "Advanced",
        "Description": description or (source_pool or {}).get("Description", "The entire H3VR vanilla arsenal"),
        "EnemyProgressionType": enemy_progression_type if enemy_progression_type is not None else (source_pool or {}).get("EnemyProgressionType", 0),
        "Enemies": [
            {"EnemyName": 0, "EnemyNameString": current_enemy_type, "Value": current_value}
            for current_enemy_type, current_value in zip(effective_enemy_types, enemy_values)
        ],
        "Guns": guns,
        "Name": profile_name or (source_pool or {}).get("Name", f"All_in_One_{enemy_type}"),
        "OrderType": order_type if order_type is not None else (source_pool or {}).get("OrderType", 1),
    }


def main():
    args = parse_args()
    items = json.loads(args.input.read_text(encoding="utf-8-sig"))
    rules = load_rules(args.rules)
    source_pool = load_pool(args.source_profile)
    if args.source_profile and source_pool is None:
        raise ValueError(f"Unable to read source profile: {args.source_profile}")
    enemy_type = profile_enemy_type(source_pool, ENEMY_TYPES[args.sosig_type])
    enemy_types = args.enemy_types.split(",") if args.enemy_types else None
    enemy_values = [int(value) for value in args.enemy_values.split(",")] if args.enemy_values else None
    if enemy_types:
        enemy_types = [value.strip() for value in enemy_types if value.strip()]
        if not enemy_types:
            raise ValueError("--enemy-types must include at least one identifier")
    args.output_dir.mkdir(parents=True, exist_ok=True)
    output_name = args.output_name or f"GunGameWeaponPool_All_in_One_{enemy_type}.json"
    if Path(output_name).name != output_name:
        raise ValueError("--output-name must be a filename, not a path")
    output_path = args.output_dir / output_name
    pool = build_pool(
        items,
        enemy_type,
        output_path,
        rules,
        source_pool,
        args.profile_name,
        args.description,
        enemy_types,
        enemy_values,
        args.enemy_progression_type,
        args.order_type,
        args.seed,
    )

    if any(not gun["MagNames"] for gun in pool["Guns"]):
        raise ValueError("Generated GunGame pool contains a weapon without feed options")

    output_path.write_text(json.dumps(pool, indent=4), encoding="utf-8")
    print(f"Generated {len(pool['Guns'])} advanced vanilla firearms in {output_path.name}")


if __name__ == "__main__":
    main()
