#!/usr/bin/env python3
"""
自动化转换工坊和配方数据
- 将refs/other中的工坊数据转换为符合schema的格式
- 拆分为独立文件
- 生成配方文件
"""

import json
import os
from pathlib import Path
from typing import Dict, List, Any

# 常量：时代映射
ERA_MAP = {
    "CLASSIC": "C",
    "MEDIEVAL": "M",
    "RENAISSANCE": "R",
    "CLASSICAL": "C"
}

# TICKS_PER_SECOND常量
TICKS_PER_SECOND = 60

class WorkshopConverter:
    def __init__(self, base_dir: Path):
        self.base_dir = base_dir
        self.refs_dir = base_dir / "refs" / "other"
        self.workshops_dir = base_dir / "data" / "core" / "workshops"
        self.registries_dir = base_dir / "content" / "registries"

        # 确保目录存在
        self.workshops_dir.mkdir(parents=True, exist_ok=True)
        self.registries_dir.mkdir(parents=True, exist_ok=True)

    def convert_era(self, era: str) -> str:
        """转换时代标识"""
        return ERA_MAP.get(era.upper(), era)

    def convert_workshop_definition(self, source_data: Dict) -> Dict:
        """转换工坊定义"""
        workshops_list = source_data.get("workshops", [])
        if not workshops_list:
            return None

        workshop = workshops_list[0]  # 取第一个工坊

        result = {
            "id": workshop["id"],
            "name": workshop["name"],
            "tags": workshop.get("tags", []),
            "era_min": self.convert_era(workshop.get("era_min", "C")),
            "era_max": self.convert_era(workshop.get("era_max", "R")),
            "attachment_slots": workshop.get("attachment_slots", [])
        }

        # 可选字段
        if "description" in workshop:
            result["description"] = workshop["description"]

        # 自动推断construction_id
        workshop_id = workshop["id"]
        construction_id = workshop_id.replace("_workshop_", "_construction_workshop_")
        result["placeable_construction_id"] = construction_id

        return result

    def convert_attachments(self, source_data: Dict) -> List[Dict]:
        """转换附件定义"""
        attachments = source_data.get("attachments", [])
        result = []

        for att in attachments:
            converted = {
                "id": att["id"],
                "name": att["name"],
                "slot": att["slot"]
            }

            # 处理era字段（可能是era或era_min/era_max）
            if "era" in att:
                converted["era"] = self.convert_era(att["era"])
            if "era_min" in att:
                converted["era_min"] = self.convert_era(att["era_min"])
            if "era_max" in att:
                converted["era_max"] = self.convert_era(att["era_max"])

            # upgrade_to
            if "upgrade_to" in att:
                converted["upgrade_to"] = att["upgrade_to"]

            # tags
            if "tags" in att:
                converted["tags"] = att["tags"]

            # power_w
            if "power_w" in att:
                converted["power_w"] = att["power_w"]

            result.append(converted)

        return result

    def convert_recipe(self, recipe: Dict, workshop_id: str) -> Dict:
        """转换单个配方"""
        result = {
            "id": recipe["id"],
            "name": self.generate_recipe_name(recipe),
            "category": self.infer_category(workshop_id),
            "workshops": [workshop_id],
            "work_time": {
                "duration_ticks": recipe.get("duration_s", 60) * TICKS_PER_SECOND
            },
            "skill": self.infer_skill(workshop_id, recipe),
            "requirements": self.convert_requirements(recipe),
            "outputs": self.convert_outputs(recipe),
            "unlock": {"autolearn": True}
        }

        # 处理era
        if "era" in recipe:
            result["era"] = self.convert_era(recipe["era"])

        # 处理inputs
        if "inputs" in recipe:
            result["inputs"] = self.convert_inputs(recipe["inputs"])
        elif "inputs_or" in recipe:
            # 对于inputs_or，转换为any_of形式
            result["inputs"] = self.convert_inputs_or(recipe["inputs_or"])

        return result

    def generate_recipe_name(self, recipe: Dict) -> str:
        """从recipe id生成展示名"""
        recipe_id = recipe["id"]
        # 移除前缀
        name = recipe_id.replace("core_recipe_", "").replace("_", " ").title()
        return name

    def infer_category(self, workshop_id: str) -> str:
        """推断配方类目"""
        if "stoneworks" in workshop_id or "stone" in workshop_id:
            return "stoneworks"
        elif "metallurgy" in workshop_id or "smeltery" in workshop_id:
            return "metallurgy"
        elif "woodworking" in workshop_id or "wood" in workshop_id:
            return "woodworking"
        elif "glass" in workshop_id:
            return "glassblowing"
        elif "pottery" in workshop_id:
            return "pottery"
        elif "fuel" in workshop_id or "alkali" in workshop_id:
            return "fuel_alkali"
        else:
            return "general"

    def infer_skill(self, workshop_id: str, recipe: Dict) -> Dict:
        """推断技能要求"""
        # 技能映射
        skill_map = {
            "stoneworks": "masonry",
            "metallurgy": "smelting",
            "smeltery": "smelting",
            "woodworking": "carpentry",
            "glass": "glassblowing",
            "pottery": "pottery",
            "fuel": "labor",
            "alkali": "labor"
        }

        primary = "labor"  # 默认
        for key, skill in skill_map.items():
            if key in workshop_id:
                primary = skill
                break

        # 根据配方era推断难度
        era = recipe.get("era", "C")
        difficulty_map = {"C": 2, "M": 3, "R": 4, "CLASSIC": 2, "MEDIEVAL": 3, "RENAISSANCE": 4}
        difficulty = difficulty_map.get(era, 2)

        return {
            "primary": primary,
            "difficulty": difficulty,
            "xp_per_craft": difficulty * 5
        }

    def convert_requirements(self, recipe: Dict) -> Dict:
        """转换requirements"""
        result = {}

        if "requires_enablers" in recipe:
            result["enablers"] = recipe["requires_enablers"]
        elif "attachments_required" in recipe:
            result["enablers"] = recipe["attachments_required"]

        result["power_w"] = 0  # 默认无功率需求

        return result

    def convert_inputs(self, inputs: List[Dict]) -> List[Dict]:
        """转换inputs"""
        result = []
        for inp in inputs:
            converted = {}
            if "id" in inp:
                converted["def_id"] = inp["id"]
                converted["count"] = inp.get("count", 1)
            elif "tag" in inp:
                converted["tag"] = inp["tag"]
                converted["count"] = inp.get("count", 1)

            # charges处理
            if "charges_g" in inp:
                # 暂时忽略charges，需要特殊处理
                pass
            if "charges_ml" in inp:
                pass

            if converted:
                result.append(converted)

        return result

    def convert_inputs_or(self, inputs_or: List[List[Dict]]) -> List[Dict]:
        """转换inputs_or为any_of形式"""
        if len(inputs_or) == 1:
            return self.convert_inputs(inputs_or[0])

        # 构建any_of
        alternatives = []
        for alternative in inputs_or:
            for inp in alternative:
                alt_item = {}
                if "id" in inp:
                    alt_item["def_id"] = inp["id"]
                    alt_item["count"] = inp.get("count", 1)
                elif "tag" in inp:
                    alt_item["tag"] = inp["tag"]
                    alt_item["count"] = inp.get("count", 1)
                alternatives.append(alt_item)

        return [{"any_of": alternatives}]

    def convert_outputs(self, recipe: Dict) -> List[Dict]:
        """转换outputs"""
        outputs = recipe.get("outputs", [])
        byproducts = recipe.get("byproducts", [])

        result = []

        for out in outputs:
            converted = {}
            if "id" in out:
                converted["def_id"] = out["id"]
                converted["count"] = out.get("count", 1)
            result.append(converted)

        for by in byproducts:
            converted = {}
            if "id" in by:
                converted["def_id"] = by["id"]
                converted["count"] = by.get("count", 1)
                converted["byproduct"] = True
            result.append(converted)

        return result

    def process_workshop_file(self, source_file: Path):
        """处理单个工坊文件"""
        print(f"Processing: {source_file.name}")

        with open(source_file, 'r', encoding='utf-8') as f:
            source_data = json.load(f)

        # 转换工坊定义
        workshop_def = self.convert_workshop_definition(source_data)
        if not workshop_def:
            print(f"  [WARN] No workshop definition found")
            return

        workshop_id = workshop_def["id"]
        attachments = self.convert_attachments(source_data)

        # 生成workshop文件
        workshop_output = {
            "$schema": "../../../../content/schemas/workshops.schema.json",
            "version": "1.0.0",
            "workshops": [workshop_def],
            "attachments": attachments
        }

        output_file = self.workshops_dir / f"{workshop_id}.json"
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(workshop_output, f, indent=2, ensure_ascii=False)
        print(f"  [OK] Created: {output_file.name}")

        # 转换recipes
        recipes = source_data.get("recipes", [])
        if recipes:
            converted_recipes = []
            for recipe in recipes:
                try:
                    converted = self.convert_recipe(recipe, workshop_id)
                    converted_recipes.append(converted)
                except Exception as e:
                    print(f"  [WARN] Error converting recipe {recipe.get('id', 'unknown')}: {e}")

            # 生成recipes文件
            recipe_category = self.infer_category(workshop_id)
            recipe_output = {
                "$schema": "../../content/schemas/recipes.schema.json",
                "version": "1.0.0",
                "recipes": converted_recipes
            }

            recipe_file = self.registries_dir / f"recipes.{recipe_category}.json"
            with open(recipe_file, 'w', encoding='utf-8') as f:
                json.dump(recipe_output, f, indent=2, ensure_ascii=False)
            print(f"  [OK] Created: {recipe_file.name} ({len(converted_recipes)} recipes)")

    def run(self):
        """运行转换"""
        print("=" * 60)
        print("Workshop & Recipe Converter")
        print("=" * 60)

        # 找到所有工坊JSON文件
        workshop_files = list(self.refs_dir.glob("core_workshop_*.json"))

        print(f"\nFound {len(workshop_files)} workshop files\n")

        for workshop_file in sorted(workshop_files):
            try:
                self.process_workshop_file(workshop_file)
            except Exception as e:
                print(f"  [ERROR] Error processing {workshop_file.name}: {e}")

        print("\n" + "=" * 60)
        print("Conversion complete!")
        print("=" * 60)


if __name__ == "__main__":
    base_dir = Path(__file__).parent
    converter = WorkshopConverter(base_dir)
    converter.run()
