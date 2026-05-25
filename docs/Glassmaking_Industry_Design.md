# 玻璃业 — 设计文档（古典 / 中世纪 / 文艺复兴）

> **对应工坊**: [Glasshouse](workshops/Glasshouse.md)
> 工坊文档是 markdown 实现层 + JSON 对齐；本 industry md 提供历史锚点与设计原理。


**版本**: 1.0  **状态**: 可直接落库实现
**范围**: 玻璃熔炼、吹制 / 模铸、平板玻璃、镜子、彩窗、玻璃容器、玻璃珠串。
**Out-of-scope**: 现代浮法玻璃、光学玻璃（17 世纪后）、晚期镜面工业化。
**Goal**: 把"林业 + 化学引用了但本体不存在"的玻璃业补齐；承载阿拉伯/威尼斯/Murano 的文化感；与化学/陶瓷/建筑/科学（早期透镜）挂接。

---

## 1) 历史锚点（用于解锁与配方）

- **C（罗马）**: 玻璃吹制管发明 ~50 BC（叙利亚），罗马普及；原料 = 砂 + 埃及 natron（天然苏打）+ 石灰；色玻璃、马赛克瓦、平板铸窗（粗糙）。
- **M（中世纪 + 阿拉伯）**: 西欧 **森林玻璃 Waldglas** — 苏打缺货后改用木灰（potash），玻璃带绿/棕色；拜占庭、伊斯兰世界保留高质量苏打玻璃 + 鎏金/釉绘玻璃（Mamluk）；哥特彩窗玻璃工艺成熟（12 世纪 Chartres）。
- **R（威尼斯 + 北欧）**: **威尼斯 cristallo**（Murano, ~1450）— 用纯化苏打 + 锰澄清剂，几乎无色透明；**威尼斯镜**（锡汞齐镀背，14-16c）成为奢侈品；波西米亚/德意志 potash 玻璃做厚壁高脚杯。

---

## 2) 时代 → 工艺链 → 投入/产出 → 解锁

### 古典（C）

**A. 罗马平板玻璃（cast）+ 吹制初步**
- 链路: 砂 + natron + 石灰 → 玻璃炉熔融 → 平板铸（薄板）或吹管吹制成器
- IO: `sand ×4 + flux_natron ×1 + flux_lime ×1 + charcoal_std ×4 → glass_melt ×6` → `glass_melt ×1 → glass_pane_rough ×1` 或 `glass_vessel ×1`
- 解锁: **玻璃熔炉（C）**、**吹管台（C）**、**平板铸床（C）**
- 限制: 透明度低；杂色多；窗户带气泡

**B. 色玻璃 / 马赛克瓦**
- 链路: 玻璃熔融加金属氧化物着色 → 切块
- IO: `glass_melt ×4 + colorant ×0.5 → mosaic_tessera ×16`
- 解锁: **色彩瓦坊（C）**
- 着色剂: copper（蓝/红）、cobalt-bearing（蓝）、iron（绿）、manganese（紫）

**C. 玻璃珠（贸易品）**
- 链路: 玻璃丝绕芯 → 玻璃珠 / millefiori
- IO: `glass_melt ×1 → glass_bead ×20`
- 解锁: **珠匠台（C）**

---

### 中世纪（M）

**A. 森林玻璃 Waldglas（potash 路线）**
- 链路: 砂 + 木灰 potash + 石灰 → 林边玻璃窑 → 绿色玻璃
- IO: `sand ×4 + wood_ash ×2 + flux_lime ×1 + charcoal_std ×4 → glass_melt_green ×6`
- 解锁: **森林玻璃窑（M）**
- 注: 与林业 wood_ash 副产闭环；玻璃自带"绿色"标签

**B. 彩窗 Cathedral Glass**
- 链路: 大色玻璃板切割 → 铅条镶嵌（cames）→ 彩窗
- IO: `glass_pane_colored ×N + lead_came ×N + lead_ingot ×1 → stained_window ×1`
- 解锁: **彩窗作坊（M）**
- 用途: 教堂、贵族大宅；强烈**美观 + 文化**标签

**C. 阿拉伯/伊斯兰高档玻璃（贸易输入）**
- 链路: 进口品；不是本地生产链
- IO: 通过商队偶尔获得 `glass_islamic_enameled ×1`（高价 + 高美观，可作艺术品）
- 解锁: 默认开启，依赖与沙漠苏丹文明的贸易关系（见 CIVILIZATIONS_FACTIONS_SPEC）

**D. 中世纪窗户（crown glass / cylinder glass）**
- 链路: 大泡吹起 → 切割展平
- IO: `glass_melt ×4 + 劳力 → glass_pane_medium ×3`
- 解锁: **吹制平板工位（M）**
- 比 C 期平板：透明度 +1，废品率 ↓

---

### 文艺复兴（R）

**A. 威尼斯 cristallo（Murano 工艺）— 招牌台阶**
- 链路: 精提苏打（用 manganese 澄清）+ 海砂 + 焙烧贝壳灰 → 纯净熔体 → 极薄高脚杯
- IO: `sand_fine ×4 + soda_purified ×2 + lime_calcined ×1 + manganese_clarifier ×0.2 + charcoal_hp ×4 → glass_cristallo ×6`
- 解锁: **Murano 大窑（R）**、**苏打精提工位（R，依赖化学工坊）**
- 产物: `glass_goblet_cristallo`, `glass_pane_crystal`, `glass_decanter`
- 极高品质标签 + 极高售价 + 名声加成

**B. 威尼斯镜（mirror）**
- 链路: 极平整玻璃板 → 锡汞齐镀背
- IO: `glass_pane_crystal ×1 + tin_ingot ×0.2 + mercury ×0.3 → mirror_silvered ×1`（mercury 大部分回收，少量为 fume）
- 解锁: **镜匠工位（R，依赖化学工坊提供 mercury）**
- 用途: 奢侈品（vanity / 装饰）；可作贵族礼物

**C. 早期光学玻璃（凸透镜 / 阅读石）**
- 链路: 高纯玻璃磨制 → 简单凸透镜（reading stone, 11-13c 起；眼镜片 13-15c）
- IO: `glass_cristallo ×1 + 磨工 → lens_reading ×1`
- 解锁: **磨镜工位（R，可选）**
- 注: 严格保留为"老花眼阅读石 / 早期单镜片眼镜"层级；**不解锁望远镜/显微镜**（超出"R 早期"边界）

**D. 实验玻璃器皿（化学/炼金前置）**
- 链路: 吹制蒸馏器 / 烧瓶 / 量瓶
- IO: `glass_melt ×2 + 高级吹工 → alembic_glass ×1 / retort ×1 / flask ×3`
- 解锁: **实验玻璃工位（R）**
- 用途: 化学/炼金工坊的关键耗材；蒸馏白兰地的玻璃替代陶罐版本

**E. 波西米亚 / 德意志 potash crystal（厚壁）**
- 链路: M 期 Waldglas 工艺改良 + 高 potash 比例 → 厚壁透明高脚杯
- IO: `sand_fine ×4 + wood_ash_pure ×3 + lime ×1 + charcoal_hp ×4 → glass_bohemian ×6`
- 解锁: **Bohemian 玻璃窑（R）**
- 比 cristallo: 略偏厚重 + 适合雕刻 / 镀金；性价比更高

---

## 3) 物品（Items）

**原料**: `sand`, `sand_fine`, `flux_natron`, `flux_lime`, `wood_ash`, `wood_ash_pure`, `soda_purified`, `lime_calcined`, `manganese_clarifier`, `colorant_copper`, `colorant_cobalt`, `colorant_iron`, `colorant_manganese`, `litharge`（铅玻璃用，来自冶炼副产）
**中间体**: `glass_melt`, `glass_melt_green`, `glass_melt_lead`, `gather`（吹管头取料）
**成品（容器/平板）**: `glass_pane_rough`（C）、`glass_pane_medium`（M）、`glass_pane_crystal`（R）、`glass_vessel`、`glass_goblet`、`glass_goblet_cristallo`、`glass_decanter`、`glass_bottle`、`mirror_silvered`、`stained_window`
**装饰**: `mosaic_tessera`, `glass_bead`, `millefiori_cane`
**功能**: `alembic_glass`, `retort`, `flask`, `lens_reading`
**贸易品**: `glass_islamic_enameled`（M+；只从沙漠苏丹文明进口）
**副产**: `glass_cullet`（碎玻璃 → 回炉）、`mercury_fume`（事件）

---

## 4) 配方（按时代）

### C
1. `sand ×4 + flux_natron ×1 + flux_lime ×1 + charcoal ×4 → glass_melt ×6` @玻璃熔炉
2. `glass_melt ×1 → glass_pane_rough ×1` 或 `glass_vessel ×1` @吹管台 / 平板铸床
3. `glass_melt ×4 + colorant_* ×0.5 → mosaic_tessera ×16` @色彩瓦坊
4. `glass_melt ×1 → glass_bead ×20` @珠匠台

### M
5. `sand ×4 + wood_ash ×2 + flux_lime ×1 + charcoal ×4 → glass_melt_green ×6` @森林玻璃窑
6. `glass_melt ×4 → glass_pane_medium ×3` @吹制平板工位
7. `glass_pane_colored ×N + lead_came ×N + lead_ingot ×1 → stained_window ×1` @彩窗作坊
8. `glass_melt + litharge ×0.5 → glass_melt_lead ×1`（铅玻璃，光学 / 装饰用，可选） @森林玻璃窑

### R
9. `sand_fine ×4 + soda_purified ×2 + lime_calcined ×1 + manganese_clarifier ×0.2 + charcoal_hp ×4 → glass_cristallo ×6` @Murano 大窑
10. `glass_pane_crystal ×1 + tin_ingot ×0.2 + mercury ×0.3 → mirror_silvered ×1` @镜匠工位
11. `glass_cristallo ×1 → lens_reading ×1` @磨镜工位
12. `glass_melt ×2 → alembic_glass ×1` / `retort ×1` / `flask ×3` @实验玻璃工位
13. `sand_fine ×4 + wood_ash_pure ×3 + lime ×1 + charcoal_hp ×4 → glass_bohemian ×6` @Bohemian 玻璃窑

---

## 5) 工坊

- **C**: 玻璃熔炉、吹管台、平板铸床、色彩瓦坊、珠匠台
- **M**: 森林玻璃窑、吹制平板工位、彩窗作坊
- **R**: Murano 大窑、苏打精提工位（共化学）、镜匠工位、磨镜工位、实验玻璃工位、Bohemian 玻璃窑

---

## 6) 平衡默认值

- **燃料消耗**: 玻璃窑是仅次于冶炼的燃料大户；与林业 charcoal 闭环
- **杂质 / 透明度等级**: C = 杂色多（−2 美观）；M = 绿色调（−1 美观）；R cristallo = 无色（+2 美观）
- **威尼斯镜售价**: 极高（建议作"贵族友谊礼物"事件触发）
- **stained_window**: 教堂 / 大宅强标签，文化 + 士气 buff
- **alembic / retort**: 化学工坊 R 期可选配方依赖此
- **mercury_fume**: 镜匠工位有 5% / 季事件，需通风

---

## 7) 与其他系统的挂接

- **林业**: charcoal_std / charcoal_hp + wood_ash
- **化学/炼金**: soda_purified / lime_calcined / manganese_clarifier / mercury（关键耗材）；玻璃器皿是化学工坊本身的耗材
- **冶炼**: litharge（铅玻璃）、lead_came（彩窗）、tin/mercury（镜）
- **建筑**: stained_window 入教堂建筑；glass_pane 入民居窗户
- **贸易**: glass_islamic_enameled / cristallo 高价贸易品
- **科学（未来）**: lens_reading 是文艺复兴学院/早期科学的前置

---

## 8) 与 DF 的差异

- DF 简化为"玻璃工坊 + 沙 + 燃料 → 玻璃"；本设计拆出 **苏打 vs 木灰 vs 精提苏打** 三套时代化原料；引入 **cristallo / mirror / stained window / reading lens** 等里程碑物品
- 与林业 wood_ash 完成闭环（Waldglas 工艺核心）

---

## 9) 数据字段建议

- **glass_recipes.csv**: `id, era, flux_kind, color_tag, throughput, byproducts`
- **glass_products.csv**: `id, kind(pane/vessel/bead/mirror/lens/stain), era, beauty, fragility_tag`

---

（完）
