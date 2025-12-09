#!/usr/bin/env python3
"""
修复workshop文件结构，使其完全符合codex规范：
1. 移动文件从 data/core/workshops/ 到 content/registries/
2. 重命名为 workshops.*.json
3. 添加缺失的可选字段
4. 为attachments添加power_w字段
5. 更新$schema路径
"""

import json
import os
from pathlib import Path
import shutil

def infer_io_slots(workshop_id):
    """推断工坊的IO插槽数量"""
    # 默认值
    defaults = {
        "input_slots": 4,
        "output_slots": 4,
        "buffer_slots": 2
    }

    # 特殊工坊调整
    if "metallurgy" in workshop_id or "smeltery" in workshop_id:
        return {"input_slots": 6, "output_slots": 4, "buffer_slots": 3}
    elif "kitchen" in workshop_id or "butchery" in workshop_id:
        return {"input_slots": 6, "output_slots": 6, "buffer_slots": 2}
    elif "crafts" in workshop_id or "tailor" in workshop_id:
        return {"input_slots": 4, "output_slots": 6, "buffer_slots": 2}

    return defaults

def infer_power_w(attachment):
    """推断attachment的功率需求"""
    tags = attachment.get("tags", [])

    # 检查是否powered
    if "powered" in tags or "waterpower" in tags or "water" in tags:
        # 水力设备
        if "Medieval" in attachment.get("era", "") or attachment.get("era") == "M":
            return 150  # 中世纪水力
        elif "Renaissance" in attachment.get("era", "") or attachment.get("era") == "R":
            return 250  # 文艺复兴水力
        return 100  # 默认水力

    # 手动/人力
    return 0

def extract_domain_from_id(workshop_id):
    """从workshop id提取领域名称"""
    # core_workshop_stoneworks -> stoneworks
    # core_workshop_fuel_alkali_works -> fuel_alkali
    parts = workshop_id.replace("core_workshop_", "").split("_")

    # 特殊映射
    if "fuel" in parts and "alkali" in parts:
        return "fuel_alkali"
    elif "agri" in parts or "brew" in parts:
        return "agri_brew"
    elif "lime" in parts or "concrete" in parts:
        return "lime_concrete"
    elif "chemistry" in parts:
        return "chemistry"
    elif "pasture" in parts:
        return "pasture"

    # 取第一个有意义的词
    return parts[0] if parts else "general"

def process_workshop_file(input_path, output_dir):
    """处理单个workshop文件"""
    print(f"Processing: {input_path.name}")

    with open(input_path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    # 更新$schema路径（从4级改为2级）
    data["$schema"] = "../../content/schemas/workshops.schema.json"

    # 处理workshops数组
    for workshop in data.get("workshops", []):
        # 添加io字段
        if "io" not in workshop:
            workshop["io"] = infer_io_slots(workshop["id"])

        # 添加power_baseline_w
        if "power_baseline_w" not in workshop:
            workshop["power_baseline_w"] = 0

    # 处理attachments数组 - 添加power_w
    for attachment in data.get("attachments", []):
        if "power_w" not in attachment:
            attachment["power_w"] = infer_power_w(attachment)

    # 确定输出文件名
    if data.get("workshops"):
        workshop_id = data["workshops"][0]["id"]
        domain = extract_domain_from_id(workshop_id)
        output_filename = f"workshops.{domain}.json"
    else:
        output_filename = input_path.name.replace("core_workshop_", "workshops.")

    output_path = output_dir / output_filename

    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)

    print(f"  [OK] -> {output_filename}")
    return output_path

def main():
    base_dir = Path(__file__).parent
    input_dir = base_dir / "data" / "core" / "workshops"
    output_dir = base_dir / "content" / "registries"

    if not input_dir.exists():
        print(f"[ERROR] Input directory not found: {input_dir}")
        return

    output_dir.mkdir(parents=True, exist_ok=True)

    print("=" * 60)
    print("Workshop Structure Fix")
    print("=" * 60)
    print(f"\nInput:  {input_dir}")
    print(f"Output: {output_dir}\n")

    # 处理所有workshop文件
    workshop_files = list(input_dir.glob("core_workshop_*.json"))
    print(f"Found {len(workshop_files)} workshop files\n")

    processed = []
    for workshop_file in sorted(workshop_files):
        try:
            output_path = process_workshop_file(workshop_file, output_dir)
            processed.append(output_path)
        except Exception as e:
            print(f"  [ERROR] {workshop_file.name}: {e}")

    print(f"\n[OK] Processed {len(processed)} files")
    print(f"[OK] Output directory: {output_dir}")

    # 提示：可以删除旧目录
    print(f"\n[INFO] You can now delete the old directory: {input_dir}")
    print("=" * 60)

if __name__ == "__main__":
    main()
