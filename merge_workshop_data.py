#!/usr/bin/env python3
"""
合并placeable/workshops.json的属性到data/core/workshops/*.json文件中
生成完整的workshop定义，包含construction属性和workshop属性
"""

import json
from pathlib import Path

def main():
    base_dir = Path(__file__).parent
    placeable_file = base_dir / "data" / "core" / "placeable" / "workshops.json"
    workshops_dir = base_dir / "data" / "core" / "workshops"

    if not placeable_file.exists():
        print(f"[ERROR] Placeable file not found: {placeable_file}")
        return

    if not workshops_dir.exists():
        print(f"[ERROR] Workshops directory not found: {workshops_dir}")
        return

    # 读取placeable定义
    with open(placeable_file, 'r', encoding='utf-8') as f:
        placeable_data = json.load(f)

    # 建立ID映射：core_construction_workshop_* → placeable definition
    placeable_map = {}
    for construction in placeable_data.get("constructions", []):
        construction_id = construction.get("id", "")
        if construction_id.startswith("core_construction_workshop_"):
            placeable_map[construction_id] = construction

    print(f"\n[INFO] Found {len(placeable_map)} workshop constructions in placeable file\n")

    # 处理每个workshop文件
    workshop_files = list(workshops_dir.glob("core_workshop_*.json"))
    print(f"[INFO] Found {len(workshop_files)} workshop files\n")

    updated_count = 0
    for workshop_file in sorted(workshop_files):
        try:
            with open(workshop_file, 'r', encoding='utf-8') as f:
                workshop_data = json.load(f)

            if "workshops" not in workshop_data or len(workshop_data["workshops"]) == 0:
                print(f"[SKIP] {workshop_file.name}: No workshops array")
                continue

            workshop = workshop_data["workshops"][0]
            old_id = workshop.get("id", "")

            # 尝试多种ID匹配方式
            construction_id = None

            # 方式1: 使用placeable_construction_id字段
            if "placeable_construction_id" in workshop:
                construction_id = workshop["placeable_construction_id"]

            # 方式2: 将core_workshop_* 转换为 core_construction_workshop_*
            elif old_id.startswith("core_workshop_"):
                suffix = old_id.replace("core_workshop_", "")
                construction_id = f"core_construction_workshop_{suffix}"

            # 方式3: 基于文件名推断
            else:
                filename_base = workshop_file.stem.replace("core_workshop_", "")
                construction_id = f"core_construction_workshop_{filename_base}"

            # 查找对应的placeable定义
            if construction_id not in placeable_map:
                print(f"[WARN] {workshop_file.name}: No matching placeable for ID '{construction_id}'")
                continue

            placeable = placeable_map[construction_id]

            # 合并属性
            # 1. 统一ID为construction ID
            workshop["id"] = construction_id

            # 2. 使用placeable的category (应该都是"workshop")
            if "category" in placeable:
                workshop["category"] = placeable["category"]
            else:
                workshop["category"] = "workshop"

            # 3. 添加build_time_ticks
            if "build_time_ticks" in placeable:
                workshop["build_time_ticks"] = placeable["build_time_ticks"]
            else:
                # 默认值
                workshop["build_time_ticks"] = 7200

            # 4. 添加material_costs
            if "material_costs" in placeable:
                workshop["material_costs"] = placeable["material_costs"]
            elif "materials_required" in placeable:
                workshop["material_costs"] = placeable["materials_required"]
            else:
                # 默认材料需求
                workshop["material_costs"] = [
                    {"tag": "block", "count": 8},
                    {"tag": "plank", "count": 4}
                ]

            # 5. 添加placeable_profile
            if "placeable_profile" in placeable:
                workshop["placeable_profile"] = placeable["placeable_profile"]
            else:
                print(f"[WARN] {workshop_file.name}: No placeable_profile in placeable definition")
                continue

            # 6. 删除placeable_construction_id字段（不再需要）
            if "placeable_construction_id" in workshop:
                del workshop["placeable_construction_id"]

            # 写回文件
            with open(workshop_file, 'w', encoding='utf-8') as f:
                json.dump(workshop_data, f, indent=2, ensure_ascii=False)

            updated_count += 1
            print(f"[OK] {workshop_file.name}: Merged placeable data (ID: {construction_id})")

        except Exception as e:
            print(f"[ERROR] {workshop_file.name}: {e}")

    print(f"\n[DONE] Updated {updated_count} / {len(workshop_files)} workshop files")

    # 提示：可以备份或删除旧的placeable文件
    backup_file = placeable_file.with_suffix('.json.bak')
    if not backup_file.exists():
        import shutil
        shutil.copy2(placeable_file, backup_file)
        print(f"\n[INFO] Backup created: {backup_file}")
        print(f"[INFO] You can now delete or archive: {placeable_file}")

    print("\n" + "=" * 60)
    print("Next steps:")
    print("1. Review the updated workshop files in data/core/workshops/")
    print("2. Build and run the game to test workshop loading")
    print("3. Check logs for '[CONSTR.REG] loaded=' message")
    print("=" * 60)

if __name__ == "__main__":
    main()
