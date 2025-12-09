#!/usr/bin/env python3
"""
恢复workshop文件到正确位置：data/core/workshops/
保留之前添加的改进（io, power_w等）
"""

import json
from pathlib import Path
import shutil

def main():
    base_dir = Path(__file__).parent
    source_dir = base_dir / "content" / "registries"
    target_dir = base_dir / "data" / "core" / "workshops"

    target_dir.mkdir(parents=True, exist_ok=True)

    print("=" * 60)
    print("Restoring Workshop Files to Correct Location")
    print("=" * 60)
    print(f"\nSource: {source_dir}")
    print(f"Target: {target_dir}\n")

    # 找到所有workshops.*.json文件
    workshop_files = list(source_dir.glob("workshops.*.json"))
    print(f"Found {len(workshop_files)} workshop files\n")

    for source_file in sorted(workshop_files):
        # workshops.stoneworks.json -> core_workshop_stoneworks.json
        domain = source_file.stem.replace("workshops.", "")

        # 特殊处理
        domain_map = {
            "agri_brew": "agri_brew_works",
            "fuel_alkali": "fuel_alkali_works",
            "chemistry": "chemistry_lab",
            "pasture": "pasture_shed",
            "glass": "glass_house"
        }

        workshop_name = domain_map.get(domain, domain)
        target_filename = f"core_workshop_{workshop_name}.json"
        target_path = target_dir / target_filename

        # 读取并修正schema路径
        with open(source_file, 'r', encoding='utf-8') as f:
            data = json.load(f)

        # 改回4级路径
        data["$schema"] = "../../../../content/schemas/workshops.schema.json"

        # 写入目标位置
        with open(target_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)

        print(f"[OK] {source_file.name} -> {target_filename}")

        # 删除source文件
        source_file.unlink()

    print(f"\n[OK] Restored {len(workshop_files)} files to {target_dir}")
    print("=" * 60)

if __name__ == "__main__":
    main()
