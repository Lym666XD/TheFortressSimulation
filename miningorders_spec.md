**Mining Orders Spec / 采矿命令规格**

概述 / Overview
- 本规格描述四个采矿类命令在用户体验、数据结构、系统处理管线、执行语义、依赖和边界条件等方面的行为，作为后续重构的权威参考。
- 覆盖命令：`Dig` 挖掘、`DigStairwell` 楼梯竖井、`DigRamp` 挖斜坡、`DigChannel` 挖沟渠/通道（移除楼板并可能在下一层生成坡道）。
- 适用版本：.NET 8 + SadConsole 前端；对应代码主要位于 `HumanFortress.App` 与 `HumanFortress.Simulation` 模块。

术语 / Terminology
- Z 层（Z-level）：地图竖直方向的层级，`z=0` 为最上层，数值增大向下。
- 选择矩形（Selection Rect）：用户在平面 X-Y 上框出的包含区域，配合 Z 范围形成体积区域。
- 预检查（Precheck）：UI 与命令端避免空指令或非法目标的快速判断。
- 计划挖掘（PlannedDig, PD）：规划系统生成的待执行挖掘条目，包含坐标、Z、目标地形、优先级等。
- 任务（Job）：由 JobSystem 分配给工人的具体执行工作，包含移动、挖掘进度与地形变更。

用户流程 / UX Flow
- 打开快速菜单：`Z` → 选择 Mining 子菜单。
- 四个命令（Dig / Stairwell / Ramp / Channel）采用“第一次点击设置起点+当前 Z 作为 zMin；第二次点击设置终点+当前 Z 作为 zMax”的两步式选择。
- UI 状态：
  - 第一次点击后，`PlaceFirstCorner` 与 `PlaceZMin` 保存；进入第二次点击状态。
  - 第二次点击后，`PlaceSecondCorner` 与 `PlaceZMax` 保存；UI 计算 `rect` 与 `zMin..zMax`。
- UI 预检查：统计“可挖单元数量 validCount”，若为 0，显示 Toast 并拒绝创建命令。
- 命令入队：创建 `CreateAdvancedMiningOrderCommand`，传入 `rect`、`zMin`、`zMax`、`action`、`priority`。
- 高亮：UI 在 `zMin..zMax` 范围内对选区边框闪烁显示，内部以小点提示可能影响的单元。

数据结构 / Data Structures
- `enum MiningAction { Dig, DigStairwell, DigRamp, DigChannel, RemoveDigging }`
- `MiningAdvancedDesignation { Rect, ZMin, ZMax, Action, Priority }` 高层指令，供 Planner 展开。
- `MiningSystem.PlannedDig { Cell(X,Y), Z, GeologyHandle, TerrainKind, Priority, Seed, Action, Segment }`
  - `Segment`：`None` | `Top` | `Middle` | `Bottom`（Stairwell 专用）。
- `MiningJobSystem.ActiveMiningJob`：执行体，包含 Worker、Target、Adjacent、Stage(定位/挖掘/完成)、进度需求等。

处理管线 / Processing Pipeline
- UI → Command：`CreateAdvancedMiningOrderCommand.Execute` 在世界上下文中进行再次预检查，合规则入队 `OrdersManager.EnqueueMiningAdvanced`。
- Planner（`Simulation.Orders.MiningSystem`）读取高级指令并展开为 `PlannedDig`：
  - 轮询 `MiningAdvancedDesignation`，按 `Action` 和 `zMin..zMax` 扫描 X-Y 范围生成 PD；受每 Tick 预算 `maxPerTick` 限制。
  - 普通 `Dig` 也可来自持久 `ActiveRect` 源，公平轮询输出。
- Executor（`App.Jobs.MiningJobSystem`）吞吐 PD，并为可达目标分配工人：
  - 解析邻接（adjacency），规划路径，进入执行；不可达则回退重队列。
  - 楼梯中/底段具备层间依赖门禁：需相邻层已有楼梯。
  - 施工完成后通过 Diff 将地形改写为目标类型，同时可能投放掉落物。

命令语义 / Command Semantics
- Dig（挖掘墙/坡）
  - 目标条件：`SolidWall` 或 `Ramp`。
  - 结果：将目标单元改为 `OpenWithFloor`（如前为 `SolidWall` 则产生掉落）。
  - Z 维度：仅作用于选定的每一层（无跨层联动）。
  - 邻接：需在目标周围找到可行走邻接点；路径规划可用对角与扩圈搜索。

- DigRamp（挖斜坡）
  - 目标条件：仅 `SolidWall`。
  - 结果：将当前单元改为 `Ramp`（坡）。
  - Z 维度：在 `zMin..zMax` 的每一层，满足条件即生成 PD。
  - 邻接：与 Dig 相同，需可达邻接点。

- DigChannel（挖沟渠/拆楼板形成通天孔）
  - 目标条件：仅 `OpenWithFloor`（有地板的开放空间）。
  - 结果：
    - 当前层：改为 `OpenNoFloor`（无楼板的开放空间）。
    - 若 `z>0` 且下一层为 `SolidWall`：将下一层同坐标改为 `Ramp`，并投放掉落（来源为下层材质）。
  - 邻接：
    - 优先四邻，允许对角扩圈；若目标格本身可站立（`OpenWithFloor`），可“就地施工”（站在同格执行）。

- DigStairwell（挖楼梯竖井，贯穿多层）
  - 顶层（`zMin`）起点条件：三类可作为起始：`OpenWithFloor`、`SolidWall`、`Ramp`。
  - 展开：对 `zMin..zMax` 每层生成 PD，段位为 Top/Middle/Bottom：
    - `z==zMin` → `StairsDown`
    - `z==zMax` → `StairsUp`
    - 中间层 → `StairsUD`
  - 邻接与门禁：
    - 顶层若为 `OpenWithFloor`，可“就地施工”（站在同格）；否则需邻接可达点。
    - 中间/底层施工前需检测相邻层楼梯已存在（上或下任一层），否则 PD 暂缓并重队列。
  - 掉落：对原始 `SolidWall` 施工会产生基于地质的掉落。

放置与预检查规则 / Placement & Precheck
- UI 预检查（`FortressState.CountValidMiningCells`）
  - Dig：`SolidWall` 或 `Ramp` 记数。
  - DigRamp：仅 `SolidWall` 记数。
  - DigChannel：仅 `OpenWithFloor` 记数。
  - DigStairwell：仅统计顶层 `zMin` 的三类起点（`OpenWithFloor | SolidWall | Ramp`）。
- 命令端预检查（`CreateAdvancedMiningOrderCommand.HasAnyValidMiningCell`）一致性要求同上。

邻接与路径 / Adjacency & Pathfinding
- 邻接格可接受条件：`IsWalkable == true`（地板/坡/楼梯）。
- 查找策略：NESW → 对角 → 同心扩圈（半径至多 3）。
- 特例（可就地施工）：
  - Stairwell 顶层在 `OpenWithFloor` 上允许 `(x,y)` 自身作为邻接点。
  - Channel 在 `OpenWithFloor` 上允许 `(x,y)` 自身作为邻接点。
- 不可达时：PD 重队列，打印日志 `No adjacency ...; requeue`。

任务执行与地形变更 / Job Execution & Terrain Mutation
- Job 阶段：`ToAdj`（移动）→ `Digging`（推进若干 Tick）→ `Complete`（写 Diff）。
- Tick 需求：由内容 `tuning.mining.geology_ticks` 配置（按墙/坡类型），默认 20 Tick。
- 地形写入：
  - Dig：`SolidWall|Ramp -> OpenWithFloor`。
  - DigRamp：`SolidWall -> Ramp`。
  - DigChannel：`OpenWithFloor -> OpenNoFloor`，并可能对下层 `SolidWall -> Ramp`。
  - DigStairwell：按段位写入 `StairsDown|StairsUD|StairsUp`。
- 掉落：对 `SolidWall` 施工产生；表按地质映射，来源 `tuning.mining.geology_drops`。
- 幂等与跳过：
  - 若楼梯段目标地形已满足，日志 `Skip stairwell seg=... already ...`，直接丢弃 PD。
  - 瓶颈时会重队列以保持尝试，直至条件满足。

并发与保留 / Concurrency & Reservation
- 预防同一单元重复施工：`_reservedTiles` 标记 `(x,y,z)`；Channel 另外保留 `(x,y,z-1)`。
- 工人缺失或路径不可达：PD 回退 `_backlog` 重试，日志 `No worker ...` 或 `No adjacency ...`。

高亮与 UI 提示 / Highlights & UI
- 选区边框闪烁显示；内部点标记仅在当前 Z 层绘制。
- Toast：
  - 创建成功：`Mining order created (valid/total)`。
  - 空选区：`No diggable tiles in selection`。
- Debug/Logs：部分关键节点会输出 `[UI] Select first/second`、`[MINING] UI enqueued`、`Creating command with zMin/zMax` 等。

日志约定 / Logging Conventions
- UI 放置与命令创建：
  - `[MINING] UI enqueued action=... rect=(x,y,w x h) z=a..b validCells=n/m`。
  - `[DEBUG] Creating command with zMin=... zMax=... PlaceZMin=... PlaceZMax=...`。
- 规划与分配：
-  - `[MINING][t] Assign worker=... target=(x,y,z) adj=(ax,ay,z)`。
  - `[MINING][t] No adjacency for target=(x,y,z); requeue`。
  - 楼梯依赖：`Gate stairwell seg=Middle|Bottom ... waiting for adjacent layer stair`。
  - 幂等跳过：`Skip stairwell seg=... already ...`。

边界与容错 / Edge Cases
- 选区越界：越界单元跳过不生成 PD。
- Z 反向：`zMin/zMax` 无序时需交换，保证 `zMin <= zMax`。
- 异常或无世界 chunk：UI 渲染使用占位符 `#` 或 `?`，不生成 PD。
- 性能预算：Planner 每 Tick 输出 PD 数受 `maxPerTick` 限制（默认 128）。

重构建议 / Refactor Recommendations
- UI 与命令端预检查逻辑集中为共享库，避免两处条件漂移。
- 明确 Channel 的“就地施工”规则并在 UI 预检查中提示。
- 将楼梯依赖门禁配置化（允许上下层都可触发，或强制从顶层开始）。
- 将日志聚合到统一 Logger 通道并附加 order/job id，便于追踪。
- 在 Planner 为每类 Action 输出摘要计数日志，辅助调试（避免逐 PD 打印）。

校验清单 / Validation Checklist
- Dig：墙/坡目标能够生成 PD，工人能就近到达并将目标变为地板。
- Ramp：仅墙生成 PD，结果为坡。
- Channel：地板变通天孔，下层墙变坡；无邻接时可在同格施工。
- Stairwell：顶层可从地板/墙/坡起始；中底段受层间楼梯依赖；顶层在地板上可同格施工。

示例流程 / Example Flow
- 用户在 `z=20` 的地板上与 `z=24` 的墙体之间，框出 2x2 区域并设置 Z 范围 `20..24`，选择 `DigStairwell`。
- UI 预检仅检顶层（z=20）的三类起点；通过则入队。
- Planner 在 `20..24` 每层生成 PD（Top/Middle/Middle/Bottom）。
- JobSystem 先执行顶层（允许同格施工），随后中间/底层依赖满足后陆续执行，最终形成贯通竖井。

