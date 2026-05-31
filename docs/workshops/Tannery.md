# Tannery 制革工坊

**对应 JSON**: `data/core/workshops/core_workshop_tannery.json`（14 attachments）
**对应 industry md**: [Leatherwork_DF_Simplified_bilingual.md](../Leatherwork_DF_Simplified_bilingual.md)
**era**: C → R
**主要 tags**: workshop, leather, tanning, hides

---

## 1) 用途与定位

`raw_hide → leather` 全流程（DF 风），含树皮鞣（heavy）/ 明矾白革 / 麂皮 / 油整饰 / 精整 / R 期摩洛哥 + 科尔多瓦精品。

**轻量化原则**: 一座 Tannery 主建筑（M 批量、R 精品）+ Currier's Bench（M 油整 + R 精整）+ R 可选 Dye Bench。

**与 DF 兼容**: 保留 DF 的 `Tannery + Currier's Bench` 双工坊结构，但合并为一份 JSON + 两个 slot。

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `tan_vat` | 鞣槽 | Bark Tan Vat | Batch Tan Vat | Refined Tan Vat |
| `currier_bench` | 整饰 | — | Oiled Currier Bench | Refinement Currier Bench |
| `dye_bench` | 染色（可选）| — | — | Dye Bench |
| `dehairing_pit` | 脱毛 | Lime Pit | Improved Lime Pit | (升级) |
| `stretching_frame` | 拉伸 | Stretch Frame | (升级) | (升级) |
| `salting_bench` | 盐处理 | Salting Bench | (升级) | (升级) |

---

## 3) 配方索引（按 era）

### 3.1 古典（C）

| 配方 | 输入 | 输出 |
|---|---|---|
| Quick Tan（DF 主线）| raw_hide ×1 | leather_common ×1 |
| Bark Tan（heavy）| raw_hide ×1 + tannin_bark ×1 | leather_heavy ×1（durability+1 or 售价+10%）|
| Alum-Tawed White | raw_skin_light ×1 + alum ×1 + salt ×1 | leather_white ×1（美观+1 or 售价+10%）|

### 3.2 中世纪（M）

| 配方 | 输入 | 输出 |
|---|---|---|
| Batch Tan | raw_hide ×3 | leather_common ×3（吞吐 ×1.25）|
| Oiled Finish (Curried) | leather_* ×1 + tallow/wax ×1 | leather_*_finished ×1（耐久+1 or 售价+10%）@currier_bench |
| Suede 麂皮 | raw_skin_light ×1 + fish_oil ×1 | leather_suede ×1（舒适/美观+1）|

### 3.3 文艺复兴（R）

| 配方 | 输入 | 输出 |
|---|---|---|
| Refinement | leather_* ×1 | leather_*(quality+1) ×1 @currier_bench |
| Morocco | raw_skin_goat ×1 + sumac_tannin ×1 (+ dye_basic ×1 可选) | leather_morocco ×1（prestige，售价+25%）|
| Cordovan | raw_horse_shell ×1 + tallow/wax ×1 | leather_cordovan ×1（prestige，售价+25%）|

---

## 4) 上下游

```
[ Butchery ]
   ├─ raw_hide → Tannery (核心输入)
   ├─ raw_skin_light (羊/山羊/小牛) → Tannery (白革 / 麂皮)
   └─ raw_horse_shell (稀有) → Tannery (Cordovan)

[ Forestry / Stoneworks ]
   ├─ tannin_bark → Tannery
   └─ lime / slaked_lime → Tannery.dehairing_pit

[ Chemistry_Lab ]
   ├─ alum_crystal → Tannery (白革)
   ├─ sumac_tannin → Tannery (R Morocco)
   └─ dye_basic → Tannery R (Morocco 染色)

[ Salt_Works ]
   └─ salt → Tannery (白革 / 盐处理)

[ Butchery / Crafts ]
   └─ tallow / wax / fish_oil → Tannery (油整 / 麂皮)

[ Tannery 输出 ]
   ├─ leather_common → Tailor (服装) / Metalworks (工具配件) / Crafts
   ├─ leather_heavy → 鞋底 / 带具 / 重型护具
   ├─ leather_white → 细工手套 / 装帧（→ Paper 书皮）
   ├─ leather_suede → 软手套 / 擦拭布
   ├─ leather_morocco / leather_cordovan → 贸易奢侈品 (+25% 售价)
   ├─ hair_scrap (副产) → 毡 / Tailor
   └─ leather_scrap → 小件 / Crafts
```

---

## 5) 危害与特殊

- **stench**: workshop 唯一危害；建议通风
- **DF 兼容**: 保留 Tannery 主工坊 + Currier's Bench 双 slot，玩家 UI 体验近似 DF
- **prestige 标签**: 仅影响售价，不改战斗属性

---

## 6) 与 industry md 的对应

详细演进 + 平衡 + 历史 (Cordovan/Morocco/Aleppo): [Leatherwork_DF_Simplified_bilingual.md](../Leatherwork_DF_Simplified_bilingual.md)

（完）
