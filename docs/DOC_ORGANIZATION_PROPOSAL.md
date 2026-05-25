# 文档整理方案（供讨论）

**版本**: 1.0  **状态**: Proposal，待用户决策
**目的**: 当前根目录有 77 个 .md 散文件 + /docs/ 21 个文档 + /docs/other/ 22 个 JSON 工坊定义 + 5 个 PDF。结构松散、命名风格不一、有重复文件。本方案给出可执行的目录结构 + 命名规范 + 拆并建议，需要用户拍板。

---

## 0) 当前状况量化

| 位置 | 数量 | 实际包含 |
|---|---|---|
| 根目录 `.md` | **77** | 真 SPEC（~30）+ 阶段完结记录（5）+ UI refactor 草稿（7）+ Haul refactor（3）+ Build/Run 说明（3）+ 一次性任务（5）+ 实施状态（3）+ 研究草稿（2）+ Index/Rules 等 |
| `docs/*.md` | **18** | 行业设计（10 主流）+ Process Chain 综合 + 1 份审阅报告 + 1 份采访简报 + CONTROLS（重复）|
| `docs/*.pdf` | **5** | DF 调研 + 奇幻调研 + 六款游戏对比 |
| `docs/other/` | **23** | 1 个 CHATGPT_PROCESS_CHAIN 副本 + 22 个工坊 JSON |
| `docs/sadconsoleapi/` | 不详 | 第三方库参考 |

**主要问题**:
1. 根目录爆炸（77 个 .md）— SPEC、阶段记录、UI refactor 草稿、一次性 patch 全混在一起
2. `CONTROLS.md` 同时存在于根目录和 docs/
3. `CHATGPT_PROCESS_CHAIN.md` 同时存在于 `docs/` 和 `docs/other/`
4. 命名风格混乱：`SCREAMING_SNAKE_CASE.md`（SPEC）、`PascalCase_With_Underscores.md`（行业）、`kebab-case`、中文名混用
5. 版本号不统一：有的 `_v1`、有的没有
6. 双语策略不统一：有的 EN、有的 CN、有的 bilingual
7. INDEX.md 没跟上新增的 worldbuilding / 行业文档

---

## 1) 提案 A：彻底分层（推荐）

```
TheFortressSimulation/
├── INDEX.md                       ← 总入口（更新维护）
├── README-RUN.md                  ← 玩家如何运行
├── BUILD_README.md                ← 开发者如何编译
├── MILESTONE.md                   ← 路线图
├── RULES.md                       ← 编码总则
│
├── docs/
│   ├── arch/                      ← 引擎架构（核心 SPEC）
│   │   ├── GAME_ARCHITECTURE.md
│   │   ├── GAME_STATE_FLOW.md
│   │   ├── CONCURRENCY_MODEL.md
│   │   ├── UPDATE_ORDER.md
│   │   ├── CHUNK_AND_DATA_LAYOUT.md
│   │   ├── CHUNK_ACTOR_PROTOCOL.md
│   │   ├── DIFF_LOG_AND_MERGE_STRATEGIES.md
│   │   ├── SIM_LOD_POLICY.md
│   │   ├── ERROR_HANDLING_POLICY.md
│   │   ├── DETERMINISM_CI.md
│   │   ├── SAVE_FORMAT.md
│   │   ├── RUNTIME_PROPAGATION_REQUIREMENTS.md
│   │   └── TILES_MATERIALS_ARCHITECTURE.md
│   │
│   ├── sim/                       ← 模拟系统 SPEC
│   │   ├── TILE_SPEC.md
│   │   ├── FIELD_SPEC.md
│   │   ├── FLUIDS_SOLVER_SPEC.md
│   │   ├── NAVIGATION_SPEC.md
│   │   ├── NAVIGATION_RAMP_ADDENDUM.md
│   │   ├── JOB_SCHEDULER_SPEC.md
│   │   ├── JOBS_SPEC.md
│   │   ├── ORDERS_SPEC.md
│   │   ├── HAULING_SPEC.md
│   │   ├── STOCKPILE_SPEC.md
│   │   ├── ZONE_SPEC.md
│   │   ├── CREATURE_SPEC.md
│   │   ├── CREATURE_ITEM_MANAGER.md
│   │   ├── MININGSYSTEM_SPEC.md
│   │   ├── VEHICLE_SPEC.md
│   │   ├── DIRECTOR_SPEC.md
│   │   └── UNIFIED_WORK_SCHEDULER.md
│   │
│   ├── content/                   ← 内容数据合同
│   │   ├── MATERIALS_SPEC.md
│   │   ├── MATERIALS_DATA_CONTRACT.md
│   │   ├── ITEMS_SPEC.md
│   │   ├── RECIPE_SPEC.md
│   │   ├── BUILDABLE_SPEC.md
│   │   ├── PLACEABLE_SPEC.md
│   │   ├── CONTENT_REGISTRY_OVERVIEW.md
│   │   ├── CONTENT_BUILD_PIPELINE.md
│   │   ├── GEOLOGY_COMPILER_SPEC.md
│   │   ├── MAPGEN_PIPELINE.md
│   │   └── TUNING_FILES.md
│   │
│   ├── ui/                        ← UI 设计 SPEC
│   │   ├── UI_AND_INPUT_MODEL.md
│   │   ├── UI_SPEC.md
│   │   ├── RENDERING_SNAPSHOT.md
│   │   ├── INPUT_SPEC.md
│   │   ├── INPUT_MAPPING_DESIGN.md
│   │   └── CONTROLS.md            ← 玩家控件参考
│   │
│   ├── worldbuilding/             ← 软背景（NEW，§4 提议）
│   │   ├── WORLDBUILDING_REVIEW_v1.md     ← 总评审
│   │   ├── WORLD_LORE_SPEC.md             ← 待写
│   │   ├── PANTHEON_RELIGION_SPEC.md      ← 待写
│   │   ├── MAGIC_SYSTEM_SPEC.md           ← 待写
│   │   ├── BESTIARY_SPEC.md               ← 待写
│   │   ├── CIVILIZATIONS_FACTIONS_SPEC.md ← 待写
│   │   └── CULTURE_VALUES_SPEC.md         ← 待写
│   │
│   ├── industries/                ← 行业设计（业已存在 + 新增）
│   │   ├── Agriculture.md
│   │   ├── Husbandry.md
│   │   ├── Forestry_NavalTimber.md
│   │   ├── Mining.md                      ← 新
│   │   ├── Smelting_Metallurgy.md         ← 新
│   │   ├── Salt.md                        ← 新
│   │   ├── Glassmaking.md                 ← 新
│   │   ├── Ceramics_Pottery.md            ← 新
│   │   ├── Chemistry_Alchemy.md           ← 新
│   │   ├── Leatherwork.md
│   │   ├── Textile.md
│   │   ├── Brewing.md
│   │   ├── Cooking.md
│   │   ├── Oil_Soap_Candle.md             ← 新
│   │   ├── Woodworking.md                 ← 新（合并木工+桶匠+弓匠）
│   │   ├── Papermaking_Book.md
│   │   ├── Building_Masonry.md            ← 原 Building_Industry
│   │   ├── Stoneworks.md                  ← 从 PROCESS_CHAIN 拆出
│   │   ├── Metalworks.md                  ← 从 PROCESS_CHAIN 拆出
│   │   └── Firearms.md                    ← 从 PROCESS_CHAIN 拆出
│   │
│   ├── research/                  ← 调研报告（PDF + 调研类 md）
│   │   ├── DwarfFortress_DeepResearch.pdf
│   │   ├── DwarfFortress_Architecture_Philosophy.pdf
│   │   ├── DwarfFortress_Overview.pdf
│   │   ├── DwarfFortress_Engine_Performance.pdf
│   │   ├── SixGames_EarlyDev_Comparison.pdf
│   │   ├── Magic_Occultism_Western_Fantasy.pdf
│   │   ├── Fantasy_Creatures_Mythological_Origins.pdf
│   │   └── HUMANFORTRESS_INTERVIEW_BRIEFING.md
│   │
│   ├── plans/                     ← 路线图与设计计划
│   │   ├── DEVELOPMENT_PROCEDURE.md        ← INDEX 引用了但找不到，可能就是 MILESTONE
│   │   ├── HAUL_SYSTEM_REFACTOR_PLAN.md
│   │   ├── HAUL_SYSTEM_ARCHITECTURE.md
│   │   ├── UI_REFACTOR_PLAN.md
│   │   ├── UI_REFACTOR_PROGRESS.md
│   │   ├── UI_REFACTOR_SUMMARY_CN.md
│   │   ├── UI_ARCHITECTURE_ANALYSIS.md
│   │   ├── QUICK_START_UI_REFACTOR.md
│   │   ├── DOC_ORGANIZATION_PROPOSAL.md    ← 本文档
│   │   └── OPTIMIZATION_SUGGESTION.md
│   │
│   ├── status/                    ← 阶段记录与状态快照
│   │   ├── PHASE_A_COMPLETE.md
│   │   ├── PHASE_B_COMPLETE.md
│   │   ├── PHASE_C_COMPLETE.md
│   │   ├── PHASE_D_COMPLETE.md
│   │   ├── PHASE_E_COMPLETE.md
│   │   ├── PHASE_F_COMPLETE.md
│   │   ├── ZONE_IMPLEMENTATION_STATUS.md
│   │   ├── ZONE_IMPLEMENTATION_STATUS_EN.md
│   │   ├── TRACKING_SYSTEM_SUMMARY.md
│   │   └── VALIDATION_DOD_CHECKLIST.md
│   │
│   ├── archive/                   ← 一次性 patch / 已弃用 / 重复
│   │   ├── APPLY_INPUT_HANDLER_PATCH.md
│   │   ├── MOUSE_CLICK_FIX.md
│   │   ├── PR_DESCRIPTION.md
│   │   ├── CHATGPT_PROCESS_CHAIN.md        ← 综合版，已拆为独立 industries 后归档
│   │   ├── CONCURRENCY_RESEARCH.md         ← 已被 CONCURRENCY_MODEL 取代？待确认
│   │   ├── NAVIGATION_RESEARCH.md          ← 已被 NAVIGATION_SPEC 取代？待确认
│   │   └── TODO_CONSTRUCTION_AND_HAULING_STABILITY.md
│   │
│   └── reference/                 ← 外部参考
│       └── sadconsoleapi/         ← 已存在
│
├── content/                       ← 已有 JSON 工坊配置（建议从 docs/other 迁出）
│   └── workshops/
│       ├── core_workshop_*.json   ← 22 个
│       └── ...
│
└── src/                           ← 代码（无变化）
```

---

## 2) 提案 B：最小动作（保守，只清理）

如果不想做大动手术，至少做以下三件：

1. **删除重复**: `docs/CONTROLS.md` 与根 `CONTROLS.md` 二选一保留；`docs/other/CHATGPT_PROCESS_CHAIN.md` 删除（保留 docs/ 版）
2. **归档过期**: 在 `docs/` 下新建 `archive/`，把 UI_REFACTOR_* / HAUL_SYSTEM_REFACTOR_PLAN / TODO_* / APPLY_INPUT_HANDLER_PATCH / MOUSE_CLICK_FIX 移入
3. **PHASE_*_COMPLETE 集中**: 新建 `docs/status/`，把 6 个 PHASE_*_COMPLETE + ZONE_IMPLEMENTATION_STATUS* + TRACKING_SYSTEM_SUMMARY 移入

不动 SPEC、不动行业文档、不动 INDEX。代价小，长期问题仍在。

---

## 3) 提案 C：折中（推荐做的最低限度）

A 提案的子集，只做：
1. **新建 `docs/industries/`** 并把所有 *_Industry_Design / *_Design_v1 / *_Industry_Research 全部迁入，统一改名为 `Agriculture.md` / `Mining.md` 等简洁形式（去掉版本/语言后缀）
2. **新建 `docs/worldbuilding/`** 放未来的 WORLD_LORE / PANTHEON / MAGIC_SYSTEM / BESTIARY / CIVILIZATIONS / CULTURE_VALUES + 现有的 WORLDBUILDING_REVIEW_v1
3. **新建 `docs/research/`** 收纳所有 PDF + HUMANFORTRESS_INTERVIEW_BRIEFING
4. **新建 `docs/status/`** + **`docs/archive/`** 按提案 B 处理
5. **SPEC 留在根目录**，因为 INDEX 与代码大量引用，不轻易动它们
6. **更新 INDEX.md** 反映新位置

代价中等，得益大。

---

## 4) 命名规范建议

- **SPEC 文件**: `SCREAMING_SNAKE.md`（已有惯例，保留）
- **行业设计**: `PascalCase.md`（去掉 `_Industry_Design`、`_Design_v1`、`_Bilingual`、`_CN`、`_Simplified_Design` 这些尾巴）
  - 例: `Forestry_NavalTimber_Design_v1.md` → `Forestry_NavalTimber.md`
  - 例: `Leatherwork_DF_Simplified_bilingual.md` → `Leatherwork.md`
- **研究 / PDF**: 文件名英化（中文文件名在跨工具时容易出问题），如：
  - `矮人要塞（Dwarf Fortress）深度调研报告.pdf` → `DwarfFortress_DeepResearch.pdf`
- **草稿 / 计划**: 加 `_PLAN`/`_PROPOSAL`/`_DRAFT` 后缀
- **状态 / 阶段**: 加 `_COMPLETE`/`_STATUS` 后缀

---

## 5) 双语策略建议

当前 8 份行业文档分布：
- CN 单语: Agriculture / Husbandry / Textile / Cooking / Papermaking / Building / 新增 7 份
- Bilingual: Leatherwork / Brewing
- EN: Forestry_NavalTimber

**建议**:
- **统一为单语 CN**（与你工作语言一致），保留 ID 与 tag 名为英文（落库需要）
- Bilingual 的两份（Leatherwork / Brewing）可选择保留双语作为"参考样本"，但新增不再做双语
- 英文版 Forestry 保留原文，加 CN 副本 `Forestry_NavalTimber.md`

---

## 6) 版本号策略建议

- **SPEC**: 文件头写 `Version: 1.x`；不在文件名带版本号，因为引用变更代价大
- **行业 / 计划 / 提案**: 同上；只在文件头版本号
- **唯一例外**: `WORLDBUILDING_REVIEW_v1.md` 这种"快照"性质的文档可以带 `_v1` / `_v2`（每次大改另起新文件）

---

## 7) 工坊 JSON 处理（/docs/other/）

22 个 `core_workshop_*.json` 文件本质上是**内容数据 (content registry)**，不应该在 docs/ 下。

**建议**:
- 创建 `content/workshops/`（如果 src/ 同级没有 content/ 就先在 docs/ 下新建 `content_registries/` 作过渡）
- 把所有 JSON 移入
- 在 INDEX 中加入"内容仓 = content/" 一节

---

## 8) INDEX.md 更新建议

把 INDEX 重写为"按目录走"的索引，避免"按概念走"的旧索引必须手工同步每个文件位置：

```markdown
# Project Index

## docs/arch/ — Engine Architecture
[文件列表]

## docs/sim/ — Simulation Systems
[文件列表]

## docs/content/ — Content Data Contracts
[文件列表]

## docs/worldbuilding/ — Soft Background & Lore
[文件列表]

## docs/industries/ — Industry Designs
[文件列表]

## docs/ui/ — UI & Input
[文件列表]

## docs/plans/ — Plans & Proposals
[文件列表]

## docs/status/ — Phase Records
[文件列表]

## docs/research/ — Research Reports
[文件列表]

## docs/archive/ — Archived / Superseded
[文件列表]

## content/ — Compiled Content Registry
[文件列表]
```

---

## 9) 执行风险与代价

| 提案 | 移动文件数 | 改变 import / 引用风险 | 工时（你方 / 我方） |
|---|---|---|---|
| A 彻底分层 | ~80 文件 | 高（INDEX、CLAUDE.md 等引用） | 我方写迁移脚本 + 你方 review |
| B 最小动作 | ~12 文件 | 低 | 你方半小时 |
| C 折中（推荐） | ~30 文件 | 中 | 我方迁移 + 你方 review |

**推荐**: **C 折中**，理由：
- 主要痛点（行业文档散乱、worldbuilding 找不到家、PDF 混杂、状态/计划/草稿混杂）都解决
- 不碰 root SPEC（INDEX + 代码大量引用，动了麻烦）
- 工坊 JSON 顺手归入 content/

---

## 10) 需要你拍板的问题（5 个）

| # | 问题 | 我的建议 |
|---|---|---|
| 1 | 选 A / B / C 哪个方案？ | C |
| 2 | 行业文件名是否统一为简洁形式（去版本/语言后缀）？ | 是 |
| 3 | PDF 中文文件名是否改英文？ | 是 |
| 4 | 双语文档是否新建时只做 CN？保留旧的双语版？ | 是 |
| 5 | 工坊 JSON 是否单独建 content/ 目录从 docs/other/ 迁出？ | 是 |

回答完这 5 个问题后，我可以：
- (a) 生成完整的 `git mv` 脚本供你审；或者
- (b) 我直接动手做（Cowork 这边有文件写权限）；或者
- (c) 我把命名 / 位置确定，但具体的 mv 留给你
