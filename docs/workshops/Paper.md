# Papermaking Workshop 造纸与书籍工坊

**对应 JSON**: `data/core/workshops/core_workshop_paper.json`（16 attachments）
**对应 industry md**: [Papermaking_and_Book_Industry_Design_CN.md](../Papermaking_and_Book_Industry_Design_CN.md)
**era**: C → R
**主要 tags**: workshop, paper, writing, bookmaking

---

## 1) 用途与定位

把"羊皮纸 + 手工纸 + 抄写 / 装订 + R 期印刷术 + 行业登记"全链合并到一座工坊。

**三条招牌**:
- **C 羊皮纸 parchment**（皮纸架 + 抄写室 + 简装装订）
- **M 手工纸 rag paper**（水力纸坊 + 干燥阁 + 施胶 + 穿板装订）
- **R 印刷术 printing**（铸字台 + 印刷所 + 登记台）

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `parchment_frame` | 皮纸架 | Parchmenter's Frame | Improved Frame | (升级) |
| `scriptorium` | 抄写室 | Scriptorium | Improved Scriptorium | (升级) |
| `binding_bench` | 装订台 | Simple Binding Bench | Sewing-on-Cords Bench | Standardized Binding |
| `paper_vat` | 纸浆槽 | — | Pulp Vat | Improved Pulp Vat |
| `stamp_mill` | 水力捣槌 | — | Water Stamp Mill (制浆) | Improved Stamp Mill |
| `dry_loft` | 干燥阁 | — | Drying Loft | Vented Drying Loft |
| `sizing_vat` | 施胶槽 | — | Sizing Vat | (升级) |
| `type_caster` | 铸字台 | — | — | Type Caster (Pb-Sn-Sb) |
| `press` | 印刷压机 | — | — | Printing Press |
| `inking_bench` | 上墨台 | — | — | Inking Bench |
| `registry_desk` | 行业登记台 | — | — | Stationer's Desk |

---

## 3) 配方索引（按 era）

### 3.1 古典（C）

| 配方 | 输入 | 输出 |
|---|---|---|
| 羊皮纸 | raw_hide ×1 + lime_lye ×1 | parchment ×2 | @parchment_frame |
| 抄写抄本（手写）| parchment ×5 + flax_thread ×1 + thin_board ×2 | manuscript ×1 (手写，依匠人 Q) | @scriptorium + binding_bench |
| 卷册 quire | paper/parchment ×5 + flax_thread ×1 | quire ×1 |

### 3.2 中世纪（M）

| 配方 | 输入 | 输出 |
|---|---|---|
| **手工纸（rag）**| old_rag ×10 + water | paper_laid ×12（可带"水印"标签）@paper_vat + stamp_mill |
| 施胶（书写性+1）| paper ×10 + gelatin ×1 | paper_sized ×10 @sizing_vat |
| 干燥 | wet_paper ×N | paper ×N @dry_loft |
| **装订升级 (sewing-on-cords)** | quire ×8 + 麻绳 ×2 + 木板 ×2 + leather ×1 | bound_book ×1（耐久+）@binding_bench M |
| 抄写/彩绘 (可选)| 墨水 ×0.1 + 颜料 ×0.1 + 抄写员 | decorated_page → 提升售价 |
| 苏打 / 钾灰相关 → Fuel_Alkali 协作 | | |

### 3.3 文艺复兴（R）

| 配方 | 输入 | 输出 |
|---|---|---|
| **铅字浇铸** | type_alloy_ingot ×1 | type_set ×1（耐用度有限）@type_caster |
| **印刷** | paper ×20 + printing_ink ×1 + type_set ×1 | printed_sheet ×20 → 折帖装订 → printed_book ×2-3 @press + inking_bench |
| 装订印刷书 | printed_sheet ×N + 木板 + leather | printed_book ×N |
| 行业登记（贸易标签）| 批量印刷量 + 少量纸 / 费用 | "registered" 标签 → 交易溢价 / 扣押风险↓ @registry_desk |
| 油性印刷油墨（→ Chemistry 提供）| linseed_oil + lampblack + 树脂 → printing_ink | （在 Chemistry_Lab 完成）|

---

## 4) 上下游

```
[ Butchery / Husbandry ]
   └─ raw_hide → Paper (羊皮纸)

[ Tannery ]
   └─ leather_white / leather_common → Paper (装订封皮)

[ Tailor ]
   └─ old_rag (旧布 / 福斯丁 / 亚麻碎) → Paper (rag paper)

[ Logging / Woodworking ]
   ├─ thin_board / 木板 → Paper (装订木板)
   └─ rod_wood → cardboard 替代（如需）

[ Smeltery ]
   └─ type_alloy_ingot (Pb-Sn-Sb) → Paper (铸字)

[ Chemistry_Lab ]
   ├─ iron_gall_ink → Paper (M 抄写)
   ├─ printing_ink → Paper (R 印刷)
   ├─ alum → Paper (施胶辅料 + 抄写颜料媒染)
   ├─ pigment_* → Paper (彩绘)
   └─ gelatin / gum_arabic → Paper (施胶)

[ Fuel_Alkali_Works ]
   └─ wood_ash → Paper (碱料 + 漂白)

[ Pasture_Shed / Butchery ]
   └─ bone_glue / animal_glue → Paper (装订)

[ Paper 输出 ]
   ├─ parchment / paper_laid / paper_sized → 抄写本 / 印刷
   ├─ quire / bound_book / printed_book → 图书馆 / 贸易 / 知识系统
   ├─ manuscript（高 Q，手写）→ 收藏 / 神器
   ├─ printed_book → 大众识字提升 / 学院系统
   ├─ scroll / decorated_page → 礼仪 / 宗教
   ├─ "registered" 标签 → 贸易溢价
   ├─ paper_recycle (边料) → 回浆
   └─ type_set 损耗 → 回炉 (Smeltery)
```

---

## 5) 危害与特殊

- **施胶废液**: 需处理（事件钩）
- **印刷规模**: 与手抄相比 **产能 ×10+**；需 paper 与 ink 稳定供应
- **登记台**: 模拟 Stationers' Company 但去除真实公会名（按用户决策；改为"行业登记台"）
- **印刷品质 vs 抄本 Q**: 抄本带 Q（典藏级）；印刷书产能大但 Q 较低；两者并存

---

## 6) 与 industry md 的对应

详细历史 + 平衡 (Fabriano / Stationers' Company / Gutenberg / 装订工艺): [Papermaking_and_Book_Industry_Design_CN.md](../Papermaking_and_Book_Industry_Design_CN.md)

（完）
