# 陶瓷业 — 设计文档（古典 / 中世纪 / 文艺复兴）

> **对应工坊**: [Pottery](workshops/Pottery.md)
> 工坊文档是 markdown 实现层 + JSON 对齐；本 industry md 提供历史锚点与设计原理。


**版本**: 1.0  **状态**: 可直接落库实现
**范围**: 陶土准备、轮制 / 模制、低温陶器、高温炻器 / 锡釉陶 / maiolica；油灯、双耳瓶 amphora、容器、瓦片（瓦片已部分在建筑业）。
**Out-of-scope**: 真正硬瓷 hard-paste porcelain（Meissen 1708 起，超出 R 早期）；保留**软质瓷（Medici / 后续）**作为 R 末稀有产物。
**Goal**: 给地中海 / 阿拉伯 / 北欧文明各自的陶瓷文化感；与酿造（rock pot）、烹饪（陶罐）、贸易（amphora）、建筑（瓦片）闭环。

---

## 1) 历史锚点

- **C**: 希腊黑红绘陶（Attic black-figure / red-figure）；罗马 **terra sigillata**（红光泽量产餐具，Gaul/Hispania 大规模生产）；amphora 双耳瓶（地中海贸易标准容器，运油/酒/garum）；陶 oil lamp；陶瓦。
- **M**: 阿拉伯 **锡釉陶 / lustreware**（9c 巴格达起）→ Hispano-Moresque（西班牙 13-15c）；莱茵流域 **盐釉炻器 stoneware**（高温烧制不渗水，Westerwald 罐）；铅釉陶（民用）。
- **R**: 意大利 **maiolica** — 锡釉打底 + 彩绘 + 二次烧成，Faenza / Deruta；Medici porcelain（1575）作早期软瓷尝试（R 末稀有，不可量产）；荷兰 Delftware 是 R 末/巴洛克，不收。

---

## 2) 时代 → 工艺链 → 投入/产出 → 解锁

### 古典（C）

**A. 基础陶土准备**
- 链路: 采黏土 → 沉淀池水洗去石屑 → 陈化 → 揉泥
- IO: `raw_clay ×4 + water → clay_workable ×3`
- 解锁: **陶土池（C）**

**B. 轮制陶器（Greek wheel）**
- 链路: 陶土 → 慢轮 / 快轮 → 成型 → 阴干 → 装窑烧制
- IO: `clay_workable ×1 → pot_unfired ×1` → `pot_unfired ×8 + charcoal ×3 → pottery_basic ×7 + cullet ×1`
- 解锁: **陶轮（C）**、**烧陶窑（C，更新窑 updraft 类型）**
- 产物: `bowl`, `jug`, `cooking_pot`, `oil_lamp`, `amphora`

**C. 罗马 terra sigillata（红光泽量产）**
- 链路: 高铁黏土 → 模具压制 → 滚印纹饰 → 单次氧化烧 → 红色光泽
- IO: `clay_workable ×1 + iron_rich_slip ×0.3 → sigillata_unfired ×1` → 烧 → `tableware_sigillata ×1`
- 解锁: **模印工台（C）**、**滚印台（C）**
- 影响: 量产餐具，售价中等，美观 +1

**D. 双耳瓶 amphora（贸易容器）**
- 链路: 模制大瓶 → 烧制 → 装运油 / 酒 / garum
- IO: `clay_workable ×3 → amphora ×1`
- 解锁: 共用陶轮 + 大模具
- 物流: amphora 是酒 / 油 / 鱼酱的远程贸易标准包装

**E. 屋面陶瓦 / 砖（已部分在建筑业）**
- 与建筑业 Building_Industry 配合；本工坊提供 unfired tile + brick，建筑业的 Clamp/Updraft Kiln 完成烧制

---

### 中世纪（M）

**A. 铅釉陶（民用 / 食器）**
- 链路: pottery_basic + 铅釉浆（lead 化合物 + 砂 + 助熔剂）→ 二次烧 → 釉面陶
- IO: `pottery_basic ×1 + lead_glaze ×0.2 → glazed_pottery ×1`
- 解锁: **釉房（M）**、**二次烧窑（M）**
- 注: 铅釉有长期毒性 → 可作"事件钩"（贵族铅中毒）

**B. 锡釉陶 / Lustreware（阿拉伯传统 → 西班牙）**
- 链路: 铅釉 + 锡氧化物（不透明白底）+ 彩绘 + 金属虹彩（lustre）
- IO: `pottery_basic ×1 + tin_glaze ×0.3 + cobalt_blue ×0.1 → lustreware ×1`
- 解锁: **锡釉房（M）**、**虹彩还原窑（M）**
- 美观 +3；贸易高价；与阿拉伯文化体感对接

**C. 盐釉炻器 Salt-glazed Stoneware（莱茵传统）**
- 链路: 高纯黏土 → 高温炻烧 → 烧成中投盐 → 表面光滑硬如石
- IO: `clay_stoneware ×1 + salt_coarse ×0.1 → stoneware_pot ×1`
- 解锁: **高温窑 / 倒焰窑（M）**、**盐投料口（M）**
- 优势: 不渗水 → 完美酒酿造容器、储水 / 储油容器；强物流加成（容量 +30%）
- 注: M 期"莱茵罐"成为北方酒 / 蜜酒主流容器

**D. 中世纪屋面瓦升级**
- 与建筑业 M 期 Peg Tile Roof 联动；本工坊提供 unfired peg tiles

---

### 文艺复兴（R）

**A. Maiolica（意大利锡釉彩绘 — R 招牌）**
- 链路: 锡釉打底 → 高难度多彩绘画（钴蓝 / 锑黄 / 铜绿 / 铁红 / 锰紫）→ 二次烧
- IO: `pottery_fine ×1 + tin_glaze ×0.3 + pigment_mix ×0.5 → maiolica_piece ×1`
- 解锁: **Maiolica 彩绘工坊（R）**、**画师工位（R）**
- 美观 +4；贸易顶级
- 文化值: maiolica 是文艺复兴艺术家与赞助人系统的代表（与未来 PATRONAGE / ART_SYSTEM 挂接）

**B. 装饰瓦 / 立面赤陶（已在建筑业 R 期 Set R2）**
- 本工坊提供 unfired decor_tile / terracotta_piece；建筑业完成烧制 + 安装

**C. 软质瓷（Medici porcelain — R 末稀有探索）**
- 链路: 加入熟石膏 / 玻璃 frit → 半透明白瓷尝试 → 极高失败率
- IO: `clay_fine ×1 + glass_cullet ×0.2 + bone_ash ×0.1 → soft_porcelain_attempt ×1`（成功率 30%）
- 解锁: **软瓷实验窑（R 末，可选）**
- 注: **不可量产**；每件产物作神器级稀有品；致敬 Medici 1575 尝试

**D. Cucurbit / Alembic 陶器（化学耗材）**
- 链路: 高温炻器形态 → 化学蒸馏陶器（玻璃 alembic 之外的替代）
- IO: `clay_stoneware ×2 → ceramic_alembic ×1 / cucurbit ×1`
- 解锁: 共用 R 高温窑
- 用途: 化学 / 炼金 / 酿造 R 期 brandy 蒸馏陶器版（玻璃版更好但贵）

---

## 3) 物品（Items）

- **原料**: `raw_clay`, `clay_workable`, `clay_stoneware`（高耐温）、`clay_fine`（细瓷土）、`iron_rich_slip`, `lead_glaze`, `tin_glaze`, `pigment_mix`（cobalt/cu/fe/mn）, `glass_cullet`, `bone_ash`
- **基础成品**: `pottery_basic`, `tableware_sigillata`, `bowl`, `jug`, `cooking_pot`, `oil_lamp`, `amphora`, `rock_pot`（DF 兼容名）
- **中高档**: `glazed_pottery`, `lustreware`, `stoneware_pot`, `maiolica_piece`, `soft_porcelain_attempt`
- **建材半成品（送建筑业烧成）**: `unfired_brick`, `unfired_roof_tile`, `unfired_decor_tile`, `unfired_terracotta_piece`, `unfired_peg_tile`
- **化学/工具**: `ceramic_alembic`, `cucurbit`

---

## 4) 配方索引（按时代）

C: 1) 陶土沉淀  2) 轮制陶器烧制  3) terra sigillata 模印 + 烧  4) amphora  5) 油灯 / cooking_pot
M: 6) 铅釉  7) 锡釉 + lustreware  8) 盐釉炻器  9) 大量 unfired_peg_tile（输给建筑业）
R: 10) Maiolica 彩绘  11) decor_tile / terracotta（输给建筑业）12) 软瓷实验 13) ceramic alembic / cucurbit

---

## 5) 工坊

- **C**: 陶土池、陶轮、烧陶窑（updraft）、模印工台、滚印台
- **M**: 釉房、二次烧窑、锡釉房、虹彩还原窑、高温倒焰窑、盐投料口
- **R**: Maiolica 彩绘工坊、画师工位、软瓷实验窑（可选）

---

## 6) 平衡默认值

- **燃料消耗**: 陶瓷与玻璃/冶炼同列三大燃料消耗户
- **吞吐**: 慢轮 ×1 / 快轮 ×1.5 / 模印量产 ×2
- **美观**: basic 0 / sigillata +1 / glazed +1 / lustreware +3 / stoneware +1（功能为主）/ maiolica +4 / soft_porcelain +5
- **物流加成**: stoneware_pot 容量 ×1.3（替代木桶部分场景）；amphora 是远程贸易包装
- **失败率**: 软瓷 70% 失败 → 碎瓷 cullet 回炉

---

## 7) 与其他系统的挂接

- **建筑**: unfired 砖 / 瓦 / decor_tile 系列全部输给建筑业烧制 + 安装
- **酿造**: rock_pot, jug, amphora 是低成本酒类容器（vs 木桶）
- **烹饪**: cooking_pot, oil_lamp（夜间照明）、tableware（餐厅 +美观）
- **贸易**: amphora 作油 / 酒 / 鱼酱标准远程包装；maiolica / lustreware 作奢侈贸易品
- **化学/炼金**: ceramic_alembic / cucurbit；颜料是关键
- **冶炼**: 提供釉料金属（lead, tin, cobalt-bearing）
- **采矿**: raw_clay 来源
- **林业**: 燃料

---

## 8) 与 DF 的差异

- DF 陶器较单调；本设计补齐 **terra sigillata / lustreware / stoneware / maiolica / soft_porcelain** 五个里程碑
- 让 amphora / stoneware_pot 拥有强物流优势，与酿造 / 贸易闭环
- 阿拉伯 lustreware → R 意大利 maiolica 的传承线提供文化感与贸易剧本

---

## 9) 数据字段建议

- **clay_types.csv**: `id, fire_temp_tier, shrinkage, color_tag`
- **ceramic_recipes.csv**: `id, era, inputs, outputs, kiln_temp, glaze_kind, time`
- **ceramic_products.csv**: `id, kind(tableware/container/tile/decor/lab), beauty, capacity_mult, fragility`

---

（完）
