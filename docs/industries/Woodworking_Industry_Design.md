# 木工业（含细木匠 / 桶匠 / 弓匠 / 弩匠）— 设计文档（古典 / 中世纪 / 文艺复兴）

> **对应工坊**: [Woodworking](../workshops/Woodworking.md) · [Crafts_Lapidary](../workshops/Crafts_Lapidary.md)
> 工坊文档是 markdown 实现层 + JSON 对齐；本 industry md 提供历史锚点与设计原理。


**版本**: 1.0  **状态**: 可直接落库实现
**范围**: 把"细木匠 joinery"、"桶匠 cooperage"、"弓匠 / 弩匠 bowyer"三个本应分离的小工艺**合并到一个木工业文档**，保持轻量。
**与林业的边界**: 林业（已存在）负责 **采伐 → 木材分级 → seasoning → 板材**；本工坊从 **timber / boards / rodwood** 开始制造**家具、容器、工具柄、桶、轻型机械部件、弓 / 弩**。
**Out-of-scope**: 大型造船（留待 SHIPBUILDING_SPEC）；大型水力 / 风力机械（留待 MECHANICAL_ENGINEERING_SPEC）。
**Goal**: 一份文档解决三类小工艺，**不刻意细化**。所有节点保持"少表项、好落库"。

---

## 1) 历史锚点

- **C 罗马**: 凿—锯—刨—鉆—车（pole lathe），mortise-tenon joints 标准化；coopering 已经使用 staves + iron hoops；composite bow（希腊 / 斯基泰 / 东方）；罗马大盾框木；wooden buckets / chests / chariot frames。
- **M**: 大教堂屋顶 carpenters 巅峰（hammerbeam, truss）；frame-and-panel joinery（避免木材冷暖收缩裂）；turners' guild；white cooper / wet cooper / dry cooper 公会分工；英格兰长弓 yew longbow（12-15c）；阿尔卑斯地区轻型 crossbow stock 制作。
- **R**: 意大利 intarsia / marquetry（镶嵌艺术）；fine cabinetry 兴起；early veneer；标准化 crossbow stock + 钢制扳机（接金属工坊）；钉子 / 螺钉技术（接金属工坊）。

---

## 2) 总体结构（一份文档三条小线）

| 子线 | 工作站 | 主要产物 |
|---|---|---|
| 通用木工 / 细木匠 | Carpenter's Bench（C/M/R） | 家具、门、地板、容器、工具柄、屋顶 |
| 桶匠 Cooperage | Cooper's Bench（C/M/R） | 桶、酒桶、水桶、储粮箱 |
| 弓匠 / 弩匠 Bowyer / Fletcher | Bowyer's Bench（C/M/R） | 木弓本体、弩臂木件、箭杆、弩矢杆 |

**三个工作站可以共建在同一栋木工房内**，但有不同的工具附件门槛（C/M/R 升级路径）。

---

## 3) 时代 → 工艺链 → 投入/产出 → 解锁

### 古典（C）

**A. 木工 / 细木匠**
- 链路: 板材 / 木材 → 凿 + 手锯 + 简单刨 → 家具 / 容器
- IO 示例:
  - `boards_oak ×3 → wooden_chest ×1`
  - `boards_oak ×2 → wooden_door ×1`
  - `boards_oak ×1 → wooden_bucket ×1`（无金属箍）
  - `timber_oak ×1 → tool_handle ×8`
- 解锁: **Carpenter's Bench (C)**

**B. 桶匠（罗马式 staves + 木 / 早期金属箍）**
- 链路: 板材 → 削桶板 stave → 弯曲 → 桶箍 → 装配
- IO: `boards_oak ×4 + iron_ingot ×0.2 → barrel_small ×1`
       `boards_pine ×3 → bucket ×1`（无金属箍，仅木）
- 解锁: **Cooper's Bench (C)**

**C. 弓匠（弓本体）**
- 链路: 取弓胚（直纹木）→ 削 → 阴干 → 弦
- IO: `bow_stave ×1 + sinew_string ×1 → short_bow ×1`
- 解锁: **Bowyer's Bench (C)**
- 注: composite bow（角 / 筋 / 木复合）需要畜牧业的 horn / sinew

---

### 中世纪（M）

**A. 木工 / 细木匠（mortise-tenon + frame-panel）**
- 升级: M Carpenter's Bench 解锁 **凿模具 + 刨床 + 木锯木台**
- IO 示例（M 替换或新增）:
  - `boards_oak ×4 → wooden_table ×1`（M 起带 frame-panel，不易裂）
  - `boards_oak ×6 → wooden_wardrobe ×1`
  - `boards_oak ×3 → wooden_door_panel ×1`（外观+1）
- 影响: 家具 +1 美观 / 耐久

**B. 桶匠（标准化 + 铁箍）**
- IO: `boards_oak ×5 + iron_hoop ×3 → barrel_wine ×1`（防漏，wine/beer 必备）
       `boards_oak ×6 + iron_hoop ×4 → barrel_large ×1`（大酒桶）
- 解锁: **Cooper's Bench (M)**
- 关键: 与酿造 M 期"修道院啤酒 / 苹果酒"挂接（必需 barrel_wine 容器）
- 分工 tag: white_cooper（干货桶）/ wet_cooper（液体桶）— 不开为独立建筑，只作产物 tag

**C. 弓匠（英式长弓 / 复合弓 / 早期弩臂）**
- IO:
  - `bow_stave_yew ×1 + sinew_string ×1 → long_bow_yew ×1`（英式）
  - `horn ×2 + sinew ×2 + boards_pine ×1 → composite_bow ×1`（草原 / 中东风格）
  - `boards_oak ×1 + crossbow_lock_kit ×1 → crossbow_stock ×1`（弩枪托木件，金属扳机由金属工坊产）
- 解锁: **Bowyer's Bench (M)**
- 注: 长弓与复合弓由文明不同自带偏好（接 CIVILIZATIONS_FACTIONS_SPEC）

**D. 箭杆 / 弩矢杆 / 羽翎**
- IO:
  - `rodwood ×1 → arrow_shaft ×20`
  - `rodwood ×1 → bolt_shaft ×20`（短粗）
  - `goose_feather ×1 → fletching ×30`（来自畜牧）
- 解锁: 共用 Bowyer's Bench

---

### 文艺复兴（R）

**A. Intarsia / Marquetry 镶嵌**
- 链路: 多种色木薄片切拼 → 几何 / 写实图案 → 贴附家具表面
- IO: `boards_walnut ×1 + boards_maple ×1 + boards_ebony_traded ×0.5 → intarsia_panel ×1`
- 解锁: **Marquetry Bench (R)**
- 用途: 高档家具 / 教堂诗班椅 / 贵族大宅
- 美观 +4 / 售价 +50%

**B. 标准化 crossbow + early wheel-lock pistol stock**
- IO:
  - `boards_oak ×1 + crossbow_lock_kit_R ×1 → crossbow_stock_R ×1`（精度 +1）
  - `boards_walnut ×1 + 紧固件 → musket_stock ×1`（与火器工坊 R 期火枪共用）
  - `boards_walnut ×1 + 紧固件 → pistol_stock ×1`（火轮式手枪木托）
- 解锁: **Bowyer's Bench (R)** 升级为 **Bowyer & Stockmaker Bench (R)**

**C. 早期 veneer（贴皮）**
- 链路: 木块切薄片 → 胶贴
- IO: `timber ×1 → veneer_sheet ×3` 用以包覆 cheaper wood
- 解锁: 共用 Marquetry Bench

**D. 高档桶（葡萄酒陈酿用）**
- IO: `boards_oak_seasoned ×6 + iron_hoop ×4 → barrel_aging_oak ×1`（陈酿用，提升酒品质 +1）
- 解锁: Cooper's Bench (R) 标记 + seasoned 木材
- 接酿造 R 期：陈酿 wine / brandy 必备

---

## 4) 物品（Items，最小集合）

**输入（来自林业 / 畜牧 / 金属 / 化学）**:
- `boards_oak / pine / maple / walnut / mixed`, `boards_oak_seasoned`, `timber_*`, `rodwood`
- `bow_stave / bow_stave_yew`, `horn`, `sinew_string / sinew`, `goose_feather`
- `iron_ingot`, `iron_hoop`, `crossbow_lock_kit / _R`, `glue_bone`（畜牧）, `linseed_oil_boiled`（化学 R，木材保护）
- `boards_ebony_traded`（贸易输入）

**家具与日用木器**:
- `wooden_chest`, `wooden_door`, `wooden_door_panel`, `wooden_bucket`, `wooden_table`, `wooden_wardrobe`, `wooden_chair`, `wooden_bed`, `wooden_shelf`
- `tool_handle`（工具杆 — 给所有工具 / 武器装柄使用）

**桶**:
- `barrel_small`, `bucket`, `barrel_wine`, `barrel_large`, `barrel_aging_oak`

**弓 / 弩 / 箭**:
- `short_bow`, `long_bow_yew`, `composite_bow`, `crossbow_stock / _R`, `arrow_shaft`, `bolt_shaft`, `fletching`
- `musket_stock`, `pistol_stock`（R）

**装饰**:
- `intarsia_panel`, `veneer_sheet`

---

## 5) 配方索引（按时代）

C: 1) chest 2) door 3) bucket 4) tool_handle 5) barrel_small 6) short_bow 7) arrow_shaft
M: 8) table / wardrobe / panel_door 9) barrel_wine / barrel_large 10) long_bow_yew / composite_bow / crossbow_stock 11) bolt_shaft / fletching
R: 12) intarsia_panel 13) veneer_sheet 14) crossbow_stock_R 15) musket_stock / pistol_stock 16) barrel_aging_oak

---

## 6) 工坊

- **Carpenter's Bench (C → M → R)**: 家具 / 容器 / 工具柄
- **Cooper's Bench (C → M → R)**: 桶
- **Bowyer's Bench (C → M → R)**（R 期改名 Bowyer & Stockmaker）: 弓 / 弩 / 箭杆 / 火器木托
- **Marquetry Bench (R)**: 镶嵌

**实施建议**: 在游戏 UI 中只暴露 **木工房** 主建筑；上面三 + 一个工作站作为内部工具附件升级，避免"建筑列表爆炸"。

---

## 7) 平衡默认值

- **木材种类 → 用途偏好**:
  - oak: 家具 + 桶 + 长弓
  - pine: 屋面 / 简单容器 / 弩臂 / 箭杆
  - walnut: R 高档家具 / 枪托
  - yew: 长弓首选
  - 复合弓需 horn + sinew + 简单木
- **桶箍**: M 起强制 iron_hoop（不再纯木箍）
- **C/M/R 吞吐倍率**: 1.0 / 1.2 / 1.4（每升级一档）
- **美观**: basic 0 / 高级 +1 / intarsia +4
- **arrow / bolt 与弓 / 弩配对**: 弓配 arrow_shaft，弩配 bolt_shaft，**不通用**（避免乱）

---

## 8) 与其他系统的挂接

- **林业**: 唯一木材来源；boards / timber / rodwood / bow_stave
- **畜牧**: horn / sinew / glue_bone / goose_feather
- **金属工坊**: iron_hoop（M 起）、crossbow_lock_kit、弓 / 弩头（箭头 / 弩矢头）、火器木托对应金属配件
- **化学**: linseed_oil_boiled 木材保护涂料（R）
- **酿造**: barrel_wine / barrel_large / barrel_aging_oak 是必需容器
- **建筑**: door / panel_door 是建筑组件；木屋顶桁架（M 大教堂级）属于建筑业的木构 set，本工坊提供构件
- **火器工坊**: musket_stock / pistol_stock / crossbow_stock_R 是火器组装关键
- **CIVILIZATIONS_FACTIONS_SPEC**: 文明偏好（草原游牧偏 composite_bow，英式分支偏 long_bow_yew）

---

## 9) 与 DF / PROCESS_CHAIN 的差异

- DF 把木工 / 桶匠 / 弓匠分成多个工坊；本设计**合并到 1 主建筑 + 3 工作站附件 + 1 R 装饰工坊**，避免建筑泛滥
- 明确"弓 / 弩 / 火器的木件全部在木工房产出，金属件在金属工坊产出，最终装配可在木工或金属工任一处完成"——简化 DF 多工坊分散的问题

---

## 10) 数据字段建议

- **wood_products.csv**: `id, category, era, beauty, weight, requires_wood_kind, requires_attachment`
- **woodwork_recipes.csv**: `id, era, inputs, outputs, station, time`

---

（完）
