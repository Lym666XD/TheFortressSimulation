#!/usr/bin/env python3
"""
合并stoneworks和lime_concrete_yard的工坊和配方
"""

import json
from pathlib import Path

TICKS_PER_SECOND = 60

def main():
    base_dir = Path(__file__).parent
    refs_dir = base_dir / "refs" / "other"
    workshops_dir = base_dir / "data" / "core" / "workshops"
    registries_dir = base_dir / "content" / "registries"

    # 读取原始数据
    with open(refs_dir / "core_workshop_stoneworks.json", 'r', encoding='utf-8') as f:
        stoneworks_data = json.load(f)

    with open(refs_dir / "core_workshop_lime_concrete_yard.json", 'r', encoding='utf-8') as f:
        lime_data = json.load(f)

    # 合并工坊定义
    workshop = stoneworks_data["workshops"][0]
    lime_workshop = lime_data["workshops"][0]

    # 合并tags和attachment_slots
    combined_tags = workshop.get("tags", []) + lime_workshop.get("tags", [])
    combined_slots = workshop.get("attachment_slots", []) + lime_workshop.get("attachment_slots", [])

    merged_workshop = {
        "id": "core_workshop_stoneworks",
        "name": "Stoneworks & Builder's Yard",
        "description": "Workshop for stonework AND building materials (lime, concrete, mortar)",
        "tags": combined_tags,
        "era_min": "C",
        "era_max": "R",
        "attachment_slots": combined_slots,
        "placeable_construction_id": "core_construction_workshop_stoneworks"
    }

    # 合并attachments
    combined_attachments = convert_attachments(stoneworks_data.get("attachments", [])) + \
                           convert_attachments(lime_data.get("attachments", []))

    # 生成合并后的工坊文件
    merged_workshop_file = {
        "$schema": "../../../../content/schemas/workshops.schema.json",
        "version": "1.0.0",
        "_comment": "Stoneworks merged with Lime & Concrete Yard for Builder profession",
        "workshops": [merged_workshop],
        "attachments": combined_attachments
    }

    output_file = workshops_dir / "core_workshop_stoneworks.json"
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(merged_workshop_file, f, indent=2, ensure_ascii=False)

    print(f"[OK] Merged workshop saved to: {output_file.name}")

    # 合并配方
    stone_recipes = stoneworks_data.get("recipes", [])
    lime_recipes = lime_data.get("recipes", [])

    converted_stone = []
    for r in stone_recipes:
        converted = convert_recipe(r, "core_workshop_stoneworks")
        converted_stone.append(converted)

    converted_lime = []
    for r in lime_recipes:
        converted = convert_recipe(r, "core_workshop_stoneworks")  # 改为stoneworks
        converted_lime.append(converted)

    all_recipes = converted_stone + converted_lime

    recipe_output = {
        "$schema": "../../content/schemas/recipes.schema.json",
        "version": "1.0.0",
        "_comment": "Stone + Lime/Concrete recipes for Builder profession",
        "recipes": all_recipes
    }

    recipe_file = registries_dir / "recipes.stoneworks.json"
    with open(recipe_file, 'w', encoding='utf-8') as f:
        json.dump(recipe_output, f, indent=2, ensure_ascii=False)

    print(f"[OK] Merged recipes saved to: {recipe_file.name} ({len(all_recipes)} recipes)")

    # 删除lime_concrete_yard工坊文件（已合并）
    lime_workshop_file = workshops_dir / "core_workshop_lime_concrete_yard.json"
    if lime_workshop_file.exists():
        lime_workshop_file.unlink()
        print(f"[OK] Deleted: {lime_workshop_file.name} (merged into stoneworks)")

    print("\n[DONE] Stoneworks merge complete!")


def convert_era(era):
    era_map = {"CLASSIC": "C", "CLASSICAL": "C", "MEDIEVAL": "M", "RENAISSANCE": "R"}
    return era_map.get(era.upper(), era)


def convert_attachments(attachments):
    result = []
    for att in attachments:
        converted = {
            "id": att["id"],
            "name": att["name"],
            "slot": att["slot"]
        }

        if "era" in att:
            converted["era"] = convert_era(att["era"])
        if "era_min" in att:
            converted["era_min"] = convert_era(att["era_min"])
        if "era_max" in att:
            converted["era_max"] = convert_era(att["era_max"])
        if "upgrade_to" in att:
            converted["upgrade_to"] = att["upgrade_to"]
        if "tags" in att:
            converted["tags"] = att["tags"]
        if "power_w" in att:
            converted["power_w"] = att["power_w"]

        result.append(converted)

    return result


def convert_recipe(recipe, workshop_id):
    result = {
        "id": recipe["id"],
        "name": generate_recipe_name(recipe),
        "category": "stoneworks",
        "workshops": [workshop_id],
        "work_time": {
            "duration_ticks": recipe.get("duration_s", 60) * TICKS_PER_SECOND
        },
        "skill": infer_skill(recipe),
        "requirements": convert_requirements(recipe),
        "outputs": convert_outputs(recipe),
        "unlock": {"autolearn": True}
    }

    if "era" in recipe:
        result["era"] = convert_era(recipe["era"])

    if "inputs" in recipe:
        result["inputs"] = convert_inputs(recipe["inputs"])
    elif "inputs_or" in recipe:
        result["inputs"] = convert_inputs_or(recipe["inputs_or"])

    return result


def generate_recipe_name(recipe):
    recipe_id = recipe["id"]
    name = recipe_id.replace("core_recipe_", "").replace("_", " ").title()
    return name


def infer_skill(recipe):
    era = recipe.get("era", "C")
    difficulty_map = {"C": 2, "M": 3, "R": 4, "CLASSIC": 2, "MEDIEVAL": 3, "RENAISSANCE": 4}
    difficulty = difficulty_map.get(era, 2)

    return {
        "primary": "masonry",
        "difficulty": difficulty,
        "xp_per_craft": difficulty * 5
    }


def convert_requirements(recipe):
    result = {}

    if "requires_enablers" in recipe:
        result["enablers"] = recipe["requires_enablers"]
    elif "attachments_required" in recipe:
        result["enablers"] = recipe["attachments_required"]

    result["power_w"] = 0

    return result


def convert_inputs(inputs):
    result = []
    for inp in inputs:
        converted = {}
        if "id" in inp:
            converted["def_id"] = inp["id"]
            if "count" in inp:
                converted["count"] = inp["count"]
        elif "tag" in inp:
            converted["tag"] = inp["tag"]
            if "count" in inp:
                converted["count"] = inp["count"]

        if converted:
            result.append(converted)

    return result


def convert_inputs_or(inputs_or):
    if len(inputs_or) == 1:
        return convert_inputs(inputs_or[0])

    # 构建any_of
    alternatives = []
    for alternative in inputs_or:
        for inp in alternative:
            alt_item = {}
            if "id" in inp:
                alt_item["def_id"] = inp["id"]
                if "count" in inp:
                    alt_item["count"] = inp["count"]
            elif "tag" in inp:
                alt_item["tag"] = inp["tag"]
                if "count" in inp:
                    alt_item["count"] = inp["count"]
            if alt_item:
                alternatives.append(alt_item)

    return [{"any_of": alternatives}]


def convert_outputs(recipe):
    outputs = recipe.get("outputs", [])
    byproducts = recipe.get("byproducts", [])

    result = []

    for out in outputs:
        converted = {}
        if "id" in out:
            converted["def_id"] = out["id"]
            if "count" in out:
                converted["count"] = out["count"]
        if converted:
            result.append(converted)

    for by in byproducts:
        converted = {}
        if "id" in by:
            converted["def_id"] = by["id"]
            if "count" in by:
                converted["count"] = by["count"]
            converted["byproduct"] = True
        if converted:
            result.append(converted)

    return result


if __name__ == "__main__":
    main()
