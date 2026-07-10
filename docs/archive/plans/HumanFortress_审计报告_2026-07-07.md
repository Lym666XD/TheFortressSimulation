# TheFortressSimulation (HumanFortress) 架构与代码审计报告

审计日期:2026-07-07 · 审计对象:`main` 分支 @ `e4bf91e`(refactor1 合并后)
审计方式:完整 clone 仓库,通读全部架构文档,精读约 40 个核心源文件,**实际编译整个解决方案并运行了测试套件**。

标注约定:**[已验证]** = 直接读到代码或亲自运行确认;**[推断]** = 基于代码结构的合理推断,未运行验证。

---

## 0. 总体结论(先说结论)

**这是一个工程纪律远超业余项目平均水平、但"确定性模拟"这一核心承诺目前无法兑现的项目。** 代码能编译(0 警告)、测试全绿 [已验证],模块边界管控是我在个人项目里见过最严格的之一;但在最核心的卖点——"same seed + inputs ⇒ same world hash 跨机器可复现"——上,存在 **2 个 P0 级、多个 P1 级的确定性破坏点**,其中最严重的一个(A* 墙钟时间预算)直接把机器速度写进了模拟结果里。同时,文档描述的并发架构(chunk-actor、SoA、九阶段流水线)与实际实现(3 个粗粒度系统的 `Parallel.ForEach` + 全局 DiffLog)之间存在数量级的差距,渲染线程当前在无同步的情况下直接读取活体世界状态。

各维度评分(资深标准,满分 10):

| 维度 | 评分 | 一句话理由 |
|---|---|---|
| 模块边界与分层纪律 | **8.5** | App 仅引用 Contracts+Runtime [已验证],且有可执行的边界回归测试锁死依赖方向 |
| 文档质量与诚实度 | 7.5 | 量大质高,且明确区分 target/current;但部分"current"描述仍在美化(见 §2.4) |
| 确定性(核心承诺) | **3** | 墙钟预算、缓存失效缺失、撕裂读取、排序键缺陷——多点破坏,且现有测试测不到 |
| 并发正确性 | 4 | 渲染线程读活体状态无同步;read 阶段契约靠自觉,无任何 enforcement |
| 性能与可扩展性 | 4.5 | 世界上限 256×256 硬编码;逐 tile 字典寻址;`SpawnItem` O(N) 全表扫描 |
| 测试与 CI | 4 | 7.5k 行测试但用自制 runner 靠扫输出字符串判定;**没有任何 CI 配置** [已验证] |
| 玩法系统完成度(相对 DF 目标) | 3.5 | 挖掘/搬运/建造/制作/仓储/区域链路已通;流体、温度、战斗、情绪、storyteller 均缺席 |

**最重要的三句话:**
1. 先修确定性,再谈优化——当前任何性能工作都可能被后续的确定性重构推翻。
2. "快照渲染"要么真做(每 tick 发布不可变快照),要么承认是活体读并加最低限度保护;现在是"快照的成本 + 活体读的风险"两头占。
3. Runtime 层(10.7k 行)已接近 Simulation(13.4k 行)的体量——胶水层与领域层等重,是分层纪律开始产生反噬的信号。

---

## 1. 审计方法与代码库画像

| 指标 | 数值 [已验证] |
|---|---|
| C# 源文件 | 1004 个,共 63,301 行(src/) |
| 平均文件长度 | **63 行/文件**(极度碎片化,大量 partial class) |
| 最大单文件 | `TickScheduler.cs` 445 行 |
| 各项目体量 | App 17.4k > Simulation 13.4k > **Runtime 10.7k** > Jobs 6.8k > Contracts 4.8k > Content 4.7k > Core 2.0k > Navigation 1.9k > WorldGen 1.7k |
| 内容数据 | 91 个 JSON,全部可解析 [已验证,逐个 json.load 校验] |
| 测试 | 1 个项目,13 个文件,约 7.5k 行;`dotnet build` 0 警告 0 错误,测试套件全部通过 [已验证] |
| CI | **不存在**(无 .github/workflows,尽管有 `DETERMINISM_CI.md` 规范文档)[已验证] |
| Git 历史 | 首提交 2026-06-11,约 14 个提交日;大量提交信息为 "update" |

画像:这是一个 AI 智能体深度参与开发的项目(`.claude/` 配置、`AGENT_PROMPT.md`、RULES 中直接提及 Codex 工作流)。它继承了这类开发方式的典型优点(风格统一、文档纪律、边界自动化检查)和典型缺点(文件过度碎片化、胶水层膨胀、文档描述领先于实现、局部实现是"看起来对"的占位符)。

---

## 2. 架构审计

### 2.1 分层与依赖:真实的亮点 [已验证]

依赖方向 `Contracts ← Core/Content/Navigation ← Simulation ← Jobs/WorldGen ← Runtime ← App` 在 csproj 层面属实:`HumanFortress.App.csproj` 只引用 Contracts 和 Runtime。更难得的是这条边界不是靠口头约定维持的:

- `ArchitectureBoundarySmokeTests` 用源码扫描锁死 import 方向矩阵、public surface 白名单、`InternalsVisibleTo` 友元图;
- `DeterministicAuthoritySmokeTests` 用正则禁止 save/replay/hash 权威路径中出现 `GetHashCode()`、字典 `.Keys/.Values` 枚举、`Guid.NewGuid()`——**全库确实没有一处 `Guid.NewGuid()`** [已验证 grep];
- 命令→DiffLog→post-tick applicator 的"UI 不直接改世界"纪律,在 orders/zones/stockpiles/workshops/professions 上已经真实落地。

这套"把架构规则写成可执行测试"的做法,是本项目最值得保留的资产。

### 2.2 Runtime 层膨胀:分层纪律的反噬

Runtime 项目 10,757 行,几乎追平 Simulation 的 13,425 行。它的内容是:session 门面 partial(十几个文件)、快照 builder(20+ 文件)、命令 target 角色接口、replay decode partial、composition group……全部是**搬运数据的胶水**。加上 Contracts 4.8k 行 DTO,"非领域代码"合计约 33k 行,超过领域代码(Sim+Jobs+Nav+WorldGen ≈ 23.9k)。

机制上的因果链:项目规则要求"每个读模型族一个 builder partial"、"每个命令族一个 replay factory partial"、"禁止 God Object"——这些规则单条都合理,叠加后产生了 1004 个平均 63 行的文件。**碎片化本身就是一种 God Object 的对偶病**:单文件认知负担降到了零,但跨文件追一条数据流(如一个 stockpile 命令从 UI 点击到 applicator 落地)需要穿过 8–10 个文件。对后续贡献者(包括 AI 智能体自己)而言,导航成本已经开始超过收益。

**建议**:冻结"继续拆分"类的重构批次;为主要数据流补 3–5 张时序图(文本版即可)放进 docs,比再拆 50 个 partial 更能降低理解成本。

### 2.3 并发模型:文档与实现相差一个数量级

| 文档承诺(CONCURRENCY_MODEL.md,标注 Normative) | 实际实现 [已验证] |
|---|---|
| chunk 为调度单元,per-chunk job,writes 集合不相交校验 | 调度单元是 **ITick 系统**,全局仅注册 **3 个**(BuildablePlanner / JobsOrchestrator / Sanitizer),`Parallel.ForEach` 跑 3 个元素 |
| per-chunk Diff-Log,merge 后单点写入 | **一个全局 `DiffLog`** + 每类型一个全局 mutation log,单锁 `List.Add` |
| Actor 邮箱处理跨 chunk 消息 | 不存在任何 actor/mailbox 代码 |
| 排序键 TileIndex → Priority → SystemId → **LocalSeq** | 实现无 LocalSeq(见 §3.4) |
| SoA 分层数据(L0–L7) | `TileBase[]` AoS 数组 + 稀疏 Dictionary 覆盖层 |
| 数据驱动 op 注册表("非硬编码枚举") | `DiffOpType` 是硬编码枚举;且 **Core 层硬编码了 `"Jobs.Mining"` 等字符串前缀优先级**(`DiffLog.SystemPrecedence`)——底层模块认识上层模块的名字,违反自己的分层规则 |

`ITick` 的 read/write 契约完全靠自觉:调度器不做任何 enforcement,没有 debug 断言,没有"read 阶段写入检测"。当前恰好安全(read 阶段的相互写入落在互不相交的 ConcurrentQueue 上,见 §4.6),但这是 **correct-by-luck, not by construction**。顺带指出一个语义错位:`HaulingSystem.ReadTick` 调用 `_orders.DrainHaulDesignations(...)` ——**"读"阶段在消费(mutate)订单队列**。它没出事是因为各系统 drain 的是不同集合,但"Read 阶段可以 drain"这个先例一旦被模仿到共享集合上,就是竞态。

**当前的并行度收益 ≈ 0**:3 个系统里,orchestrator 内部串行跑 5 个 planner,Sanitizer 的 ReadTick 是 no-op。`Parallel.ForEach` 提供的只有线程池开销和未来踩坑的机会。诚实的做法是:短期改回串行 foreach(行为完全一致、少一个变量),把并行化留给真正按 chunk 分片的那一天。

### 2.4 渲染"快照":名不副实的关键缺口

文档(GAME_ARCHITECTURE.md)措辞是 UI"consume Runtime-built snapshot DTO contracts"。实际数据流 [已验证]:

```
渲染线程 每帧 → FortressFrameRenderer.Render (App/Rendering/FortressFrameRenderer.cs:30)
  → runtime.GetFrameRenderData(...)
  → FortressRuntimeSessionCore(无任何 tick 锁/栅栏)
  → MapViewportSnapshotBuilder.Build(world, ...)   ← 直接遍历活体 World
```

与此同时模拟线程正在 write/post-tick 阶段改写同一个 World。这不是"快照",是**每帧在渲染线程上对活体状态做无同步的 DTO 化拷贝**。后果分三层:

1. **撕裂读取(见 §3.3)**:`TileBase` 是 10 字节结构体,非原子赋值;
2. **弱一致视图**:一帧内 terrain 来自 tick N、creatures 来自 tick N+1,DTO 的 `SnapshotMetadata.Tick` 标注的 tick 号是假的一致性承诺;
3. **两头亏**:付出了快照体系的全部成本(每帧构造 W×H 个 `MapViewportCellView` + 各 overlay DTO 树 → 稳定的 Gen0 分配压力),却没换来快照应有的隔离性。

**修复方向(按性价比排序)**:模拟线程在 post-tick 末尾发布一份不可变帧数据(双缓冲指针交换,`Volatile.Write` 发布),渲染线程只读已发布对象——这同时消灭撕裂、弱一致和"渲染线程干模拟层的活"三个问题,且改动集中在 host/session 一处。

### 2.5 规模天花板:与"超越 DF"目标的结构性矛盾

- `World` 构造函数硬编码 `sizeInChunks ∈ [2..8]` → **世界上限 256×256 tiles** [已验证 World.cs:31]。DF 常规 embark 是 3×3~6×6 个 48-tile 区块(144×144 到 288×288)且上百 z 层;"中大型模拟"至少要为 512×512+ 留结构余量。
- 每次 `World.GetTile` = ChunkKey 构造 + `ConcurrentDictionary` 哈希查找 + 结构体拷贝。A* 一次寻路数千次 GetTile,导航视图层层适配器(`WorldNavigationView → World → Chunk`)把这条最热路径拉长。DF 规模需要:调用方持有 chunk 引用、扁平数组直接寻址、或至少 span 化的批量读。
- Chunk 是 **单 z 层的 32×32 切片**(文档写的是 16×16×Zc 三维柱)。每个 z 层一个 Dictionary 条目,50 层 × 64 chunk = 3200 个字典项起步——可用,但和文档里的缓存友好叙事相反。
- Item 空间索引 `Dictionary<(int,int,int), List<Guid>>` 全世界一张表,无 chunk 归属;区域查询是逐格 TryGetValue(§4.1 的 SpawnItem 更糟,是全表扫描)。

这些都不是今天的 bug,但它们与"比 DF 更优秀的中大型模拟"这一 stated goal 存在**结构性矛盾**:数据布局的选择(字典 + 引用类型实例 + 每 tile 结构体拷贝)决定了后期无法靠局部优化补救,而项目文档里正确的那套设计(SoA、chunk 归属、dirty-scoped rebuild)写了却没实现。

---

## 3. 确定性专项审计(核心承诺的逐条检验)

项目北极星:"Deterministic & replayable across OS/CPU: same seed + inputs ⇒ same world/snapshot hashes"。以下按严重度排列。

### 3.1 【P0】A* 与 PathService 的墙钟时间预算 [已验证]

```csharp
// DeterministicAStar.FindPath 主循环内:
if (_timer.ElapsedMilliseconds > _tuning.MaxMsPerTickPathing)   // ← Stopwatch 墙钟!
    return BuildPartialPath(request, world);

// PathService.Solve:
if (_frameTimer.ElapsedMilliseconds > _tuning.MaxMsPerTickPathing)
{ _requestQueue.Enqueue(request); return NavPath.Invalid; }     // ← 推迟到下一 tick 也由墙钟决定

// NavigationTuning.cs:99
internal int MaxMsPerTickPathing { get; set; } = 3;             // ← 默认仅 3ms,负载下必然触发
```

**因果链**:机器更慢 / 一次 GC 停顿 / 开着调试器 → 同一 tick 内某条路径被截断为 partial 或被推迟 → 该 creature 当 tick 的移动不同 → 后续所有依赖其位置的规划、预约、掉落全部分叉 → world hash 不同。这不是理论风险:类里已经用了正确的确定性预算 `MaxNodesPerSearch = 10000`,墙钟检查是画蛇添足且致命的那一笔。**类名叫 DeterministicAStar,是全库最名不副实的一个标识符。**

修复:删除两处 `ElapsedMilliseconds` 判断;跨 tick 的负载均衡改用"每 tick 最多展开 N 个节点 / 求解 M 条路径"的确定性配额,超额请求按确定性顺序(而非 ConcurrentQueue 到达序)排队。

### 3.2 【P0】PathCache 失效机制形同虚设 [已验证]

- `PathService.InvalidateChunk` 在整个 src/ 中**外部调用次数 = 0**(grep 证实);post-tick 的 `RebuildDirtyNavigationChunks` 只重建 nav 数据,不动路径缓存。
- 唯一的失效途径是 cache key 里混入了 **仅 source 与 destination 两个 chunk** 的 ConnectivityVersion(PathService.cs:124–138)。**路径中段任何 chunk 的地形变化都不会使缓存失效。**

后果分两层:
1. *玩法层*:好在 `MovementExecutor.UpdateMovement` 每步都用 `world.IsWalkable` 校验下一格 [已验证],所以不会穿墙,只会退化为卡顿+重规划;新挖通的捷径则会被陈旧缓存**无限期忽略**(直到 LRU 恰好逐出)。
2. *确定性层(更严重)*:缓存是行为可见的状态,却不在 save/replay 权威里。存档后重载 → 冷缓存重算出正确新路径;原会话 → 热缓存继续走旧路径。**同一命令流,两个世界演化**。项目 RULES 自己写了"可重建索引不入 hash,除非成为 behavior-affecting state"——路径缓存恰恰已经是 behavior-affecting 却被当作纯派生数据。

修复:post-tick 拿到 dirty chunk 集合后逐一调用 `InvalidateChunk`(它连索引 `_chunkIndex` 都已实现,只差一行接线);同时把 key 中的双端版本号机制保留为防御。更彻底的方案是路径携带"经过的 chunk 版本向量"在使用时校验。

### 3.3 【P1】`TileBase` 撕裂读取:被注释掩盖的内存模型错误 [已验证]

```csharp
/// Size: 10 bytes ... Immutable for thread safety - use atomic replacement for updates.
internal readonly struct TileBase { ... }        // 10 字节:ushort×3 + byte×4

// Chunk.GetTile —— 注释声称 "Thread-safe for reads",实际无任何同步:
public TileBase GetTile(int x, int y) { ... return _tiles[index]; }
// Chunk.SetTile —— 持 _writeLock 写:
lock (_writeLock) { _tiles[index] = tile; ... }
```

**"readonly struct = 线程安全"是错误推理**:CLR 只保证 ≤ 指针宽度(64 位)的对齐读写原子性。10 字节结构体的数组元素赋值是多字拷贝;渲染线程(§2.4)与模拟线程并发时,可以读到 `GeoMatId` 是新值而 `TerrainBits` 是旧值的**混合 tile**。撕裂结果会流入:渲染 glyph(一帧闪错,无害)、tile inspection 弹窗、以及**放置预览/命中测试→命令构造**(放置合法性可能基于幻影 tile 判定,低概率但不可测试复现)。同类问题:`GetTilesCopy()` 无锁 `Array.Copy`;`GetFurniture` 虽持锁但返回的 `FurnitureCell` 是活体可变对象,其 `Passables` 列表随后仍会在锁内被改——**锁只保护了取引用的一瞬间,是安全剧场**。

修复选项(与 §2.4 的双缓冲快照一并解决最划算):(a) 每 tick 发布不可变帧数据后渲染线程不再触碰 Chunk;(b) 若必须活体读,把 TileBase 压进单个 `ulong`(当前 80 bit,把 `TrafficCost` 移出热结构即可塞下)配 `Volatile.Read/Write`;(c) 每 chunk seqlock。

### 3.4 【P1】DiffLog 排序键与合并语义缺陷 [已验证]

```csharp
public readonly ulong SortKey =>
    ((ulong)(uint)Target.ChunkId << 32) |
    ((ulong)(ushort)Target.LocalIndex << 16) |
    ((ulong)(byte)Priority << 8) |
    StableSystemHash8(SystemId);          // ← FNV 截断到 8 bit,256 个桶
```

三个独立问题:

1. **规范要求的 LocalSeq 不存在。** 同一系统对同一 tile 发出的两个同类 op,靠 `CompareOps` 的兜底比较链(… → `Args.CompareTo`)决出"胜者"。结果是确定的,但语义是**"打包参数数值更大的 op 赢"**——与发出顺序/意图完全无关。系统先 emit SetTerrain(挖开) 再 emit SetTerrain(回填),最终落地哪个取决于两个 ulong 谁大。这是"确定性的错误"。
2. **8-bit 系统哈希**:约 19 个系统 ID 即有 50% 碰撞概率(生日界)。碰撞不破坏确定性(后续 `string.CompareOrdinal` 兜底),但让"Priority 之后按 SystemId 排"这条规范键名存实亡,并且 `SortKey` 是**每次比较都重算的属性**——排序 N 个 op 要做 O(N log N) 次字符串 FNV 哈希,热路径白烧 CPU。
3. `Priority` 强转 `(byte)`:负优先级或 >255 直接回绕,静默改变排序。

修复:DiffOp 增加 `int LocalSeq`(每系统每 tick 单调计数)进入比较链;SystemId 改为启动期注册的 `ushort` 数值 ID(顺带解决 Core 硬编码 "Jobs.Mining" 字符串的分层违规);SortKey 在构造时预计算缓存。

### 3.5 【P1】32-bit EntityId 截断:随物品数增长的静默合并炸弹 [已验证]

```csharp
public static uint EntityId(Guid value) => BitConverter.ToUInt32(value.ToByteArray(), 0);
```

实体作用域的 diff(MoveItem/MarkCarried/MoveCreature…)按 `(Op, EntityId)` 去重取单一胜者。EntityId 是 GUID 的**前 4 字节**。生日碰撞:n 个活体物品同 tick 参与 diff 时,任意两物品 ID 碰撞概率 ≈ n²/2³³ —— **3 万件物品 ≈ 10%,10 万件 ≈ 69%**。碰撞的表现是:两件不相干物品的移动/携带 diff 被合并,其中一件**静默不动**。以 DF 体量(数万物品是常态)这不是尾部风险,是必然事件,且几乎不可能从现象反查到这行代码。修复:EntityId 升到 64 bit(GUID 前 8 字节),碰撞概率降到可忽略;或干脆用世界内单调分配的 ulong 实体 ID 取代 GUID 截断。

### 3.6 【P2】RNG 与 GUID 生成的工艺问题 [已验证]

- `DeterministicRng(ulong seed)` 的状态展开:`s2 = s0 ^ 常数`、`s3 = s1 ^ 常数` ——四个状态字只有 64 bit 有效熵且高度相关;xoshiro 作者明确建议用 SplitMix64 展开种子。8 轮 warmup 缓解但不根治相近种子的序列相关性。
- 流种子派生 `seed = seed*31 + c`(Java 字符串哈希):雪崩性差,"ai"/"aj" 两个流种子只差 1,叠加上一条,相邻命名流可能产生弱相关序列。
- `DeterministicGuidGenerator.GenerateFromPosition(tickSeed, x, y, z)`:**同 tick 同格子生成两个实体 ⇒ 完全相同的 GUID** ⇒ `Dictionary<Guid,…>` 冲突。挖掘掉落+其他 spawn 同格并非不可能。
- `BitConverter.GetBytes` 依赖主机端序——对"跨 CPU 确定性"目标,应显式 little-endian(`BinaryPrimitives`)。

### 3.7 【P2】自家规则点名过的遗漏

- `SanitizeSystem` 用**私有 `_counter`** 而非 tick 号取模决定 40-tick 节奏 [已验证 SanitizeSystem.cs:44]。存档不含该计数器 ⇒ 读档后清理相位与原会话错开 ⇒ 分叉。RULES 原文写着"Private counters/cursors that affect future deterministic ids … are authoritative replay state"——这正是被自己规则击中的实现。修复只需 `tick % _interval`。
- `MovementExecutor._movementStates`(路径、当前步、卡住计数、StepWait)是行为权威状态,不在任何 save/hash 权威里 [推断,未见对应 snapshot];读档后所有在途移动重置。
- `TickScheduler.RegisterSystem` 用 `List.Sort`(不稳定排序)排 Priority——同优先级系统的相对写序在"值语义"上未定义,当前靠注册顺序恰好稳定。加 SystemId 次级键即可根治。

### 3.8 现有确定性测试的盲区 [已验证]

`Full fortress determinism check` 的全部内容是:同进程、同机器、把 **worldgen 跑两遍**逐 tile 对比。它覆盖不到上面任何一条——墙钟预算、缓存分叉、撕裂读取、合并语义全部发生在**模拟循环 + 负载**下。`WorldReplayHashBuilder` 及 RNG 快照等基础设施其实已经相当完备,缺的只是把它们接进一个真正的模拟循环金标测试(见 §6 路线图 D1)。

---

## 4. 代码级发现(按文件)

### 4.1 `ItemManager.Spawning.cs` — 热路径 O(N) 全表扫描 ×2 [已验证]

`SpawnItem` 为找同格可堆叠物品执行 `_instances.Values.Where(逐件比对坐标)`,**而同类里就躺着 `_posIndex` 空间索引没用**;紧接着为了打诊断日志又做第二次全表扫描。挖矿是持续产生 spawn 的系统:5 万件物品时每次掉落 = 10 万次无谓遍历,聚合成平方级开销。另外方法内 8+ 处 `Emit($"...")` 的插值字符串**无论日志开关都会构造**(参数在调用前求值)——纯垃圾分配。修复:两处扫描都改走 `_posIndex`;`Emit` 改 `if (_log != null)` 守卫或 `[InterpolatedStringHandler]`。

### 4.2 `TickScheduler` — 错误处理只写了一半 [已验证]

`HandleSystemError` 捕获后仅打日志,`// TODO: Implement quarantine logic`。当前语义:系统 ReadTick 抛异常 → 其 WriteTick **仍会执行**(拿着半初始化的规划状态提交 diff),且下个 tick 继续满血参与。文档承诺的"quarantine/degrade"不存在。至少应做到:read 失败的系统跳过本 tick write,连续 K 次失败摘除并告警。

### 4.3 `ReservationManager` — 并发外衣下的竞态更新 [已验证]

`TryReserveItem` 在 `ConcurrentDictionary.AddOrUpdate` 的 **updateValueFactory 里直接 mutate 共享对象**(改 `HolderId`/`ExpireTick` 后返回原引用)。该工厂按契约可能被并发多次调用且应无副作用;两个字段的写入与随后的 `.HolderId == holderId` 判定之间无原子性。当前没炸是因为预约实际只发生在串行 write 阶段——那么正确设计是**普通 Dictionary + 阶段断言**;现在这种"到处 Concurrent 集合"的风格(OrdersManager 亦然,含枚举序不稳定的 `ConcurrentBag`)是在用线程安全容器掩盖"谁在什么阶段写"这一本应显式的所有权问题。

### 4.4 `MovementExecutor` — 双份位置真相 + 表现逻辑入侵 [已验证]

执行器在 write 阶段立即推进内部 `state.Position`,而权威 `creature.Position` 要到 post-tick 的 diff apply 才更新——同一 tick 内两个"当前位置"。实体作用域 diff 每 tick 单胜者:若两个系统同 tick 对同一 creature 发 MoveCreature,败者的内部状态即刻脱轨(靠卡住检测自愈,但这是设计脆弱点)。另:`_stepDelay = 2 // simple slowdown so movement is visible` ——**视觉节奏硬编码在确定性导航核心里**;移动速度应来自 creature 属性。

### 4.5 WorldGen — 占位实现顶着高级命名 [已验证]

`ElevationStage.SimplexNoise` / `RidgedNoise` 实际是**无插值的整数哈希白噪声**(且 `(int)(x*1619+…)` 截断造成坐标混叠),不是任何意义上的 simplex/ridged fBm——产出的"地形"是逐格随机静态而非连贯地貌。同名函数在 `FortressGenerator.TuningJson.cs`(一个按名字应该只管 JSON 的文件里!)有第二份公式略异的拷贝。`FortressMap.GetTerrainKind` 对**每个 tile 查询做 `Enum.TryParse<TerrainKind>(string)`**;`FortressChunk` 用 `ushort[,,]` 多维数组(边界检查无法消除,劣于扁平数组)。Worldgen 是"超越 DF"的支柱之一,当前整体处于占位质量,但命名让读者以为已经实现。

### 4.6 并发读阶段的"侥幸正确" [已验证]

当前 3 个并行系统在 ReadTick 里的相互写入,恰好落在 OrdersManager 内**互不相交**的 ConcurrentQueue 上(haul/mining/construction/buildable 各自独立),因此今天无内存竞态。但没有任何机制(断言/分析器/代码评审清单)阻止下一个系统在 ReadTick 里写共享状态——见 §2.3 的建议:并行度收益为零的现在,直接串行化是免费的正确性。

### 4.7 杂项(逐条,均 [已验证])

| 位置 | 问题 | 建议 |
|---|---|---|
| 仓库根目录 | 0 字节 `ProfessionAssignments` 文件、5 个一次性 Python 迁移脚本、`configs/world_map_config.txt`(0 字节)散落根目录 | 删除/移入 `tools/migrations/`,`.gitignore` 补漏 |
| 测试基建 | 自制 runner,以输出中是否出现 `"✗"/"❌ FAIL"` 字符串判定成败;无隔离、无并行、无单测过滤 | 迁移 xUnit;现有断言体几乎可机械搬运 |
| CI | `DETERMINISM_CI.md` 规范存在,workflow 不存在 | 见 §6 D1,这是全项目性价比最高的一项投入 |
| JSON 栈 | Content 用 Newtonsoft,WorldGen/Jobs 用 System.Text.Json | 统一到 System.Text.Json(source-gen 顺带提速加载) |
| `Simulation.csproj` 引用 `TheSadRogue.Primitives` | 领域核心依赖 roguelike 表现层生态的几何包,与自家"Contracts primitives"规则相悖(hash 代码里也 import 了它) | 长期换 Contracts 自有 Point/Rect;短期至少别让它出现在 save/hash 权威签名里 |
| 警告配置 | Core `TreatWarningsAsErrors=true`,Simulation `false` | 统一为 true(当前本就 0 警告,零成本) |
| 速度倍率 | 8x 档 = 2.5ms/tick 墙钟预算,tick 超时即静默掉速并重置计时 | 至少在诊断里暴露"目标 vs 实际 TPS" |

---

## 5. 与 Dwarf Fortress 目标的差距盘点

**已真实打通的链路** [已验证,通过代码 + 回归测试交叉确认]:挖掘(含 channel/ramp 足迹预约)→ 掉落解析(JSON 权重表)→ 搬运(designation → transport request → 预约 → 取放 → 仓储槽位)→ 建造(材料规划、施工场地、工坊完成通知)→ 制作(配方目录、工坊队列)→ 仓储/区域(preset 过滤器、shard 索引)→ 职业权重分配。这条竖切是项目的第二大资产(第一是边界纪律),它意味着"游戏循环骨架"是真的,不是 demo。

**结构性缺席**(对照 DF 核心体验):流体求解器(FLUIDS_SOLVER_SPEC 仅为文档)、温度/火/季节、战斗与身体部位、需求/情绪/发疯(DF 灵魂所在)、storyteller/事件导演、外部世界与来访者、完整存读档 UI(save 端口已 internal 存在但无 UI 边界)。91 个内容 JSON 与 20+ 篇产业设计文档(酿造/制陶/纺织/冶金…)显示内容侧准备远超引擎消化能力——**当前瓶颈在引擎侧,尤其是确定性地基**,而非内容或设计。

---

## 6. 整改路线图(按依赖序,非按易难)

| 级别 | 项 | 具体动作 | 预估工作量 |
|---|---|---|---|
| **P0-1** | 移除墙钟确定性污染 | 删 `DeterministicAStar`/`PathService` 两处 `ElapsedMilliseconds` 判断;跨 tick 配额改节点/请求计数;排队顺序确定化 | 0.5–1 天 |
| **P0-2** | 接通路径缓存失效 | post-tick dirty 集合逐 chunk 调 `InvalidateChunk`(索引已存在);补一条"挖通新路后路径改变"回归测试 | 0.5 天 |
| **D1** | 模拟循环确定性 CI(在 P0 后立刻做,防回归) | 新增 headless 金标测试:同 seed 建两个完整 session(含 worker+auto-dig),`ExecuteSingleTick` × 2000,比对 `WorldReplayHashBuilder` 分节 hash;矩阵:双进程 / 强制 GC / `DOTNET_TieredCompilation=0`;GitHub Actions Linux+Windows 双跑 | 2–3 天,**回报最高的一项** |
| **P1-1** | 帧快照双缓冲 | 模拟线程 post-tick 发布不可变帧数据,渲染线程只读已发布对象;同步消灭 §3.3 撕裂面 | 3–5 天 |
| **P1-2** | DiffLog 键修复 | 加 LocalSeq;SystemId 数值化(顺带清除 Core 的字符串硬编码);SortKey 预计算;Priority 域检查 | 1–2 天 |
| **P1-3** | EntityId 64 bit | `DiffTargetEncoding` + `DiffTarget` 升位;顺带修 `GenerateFromPosition` 加序列盐 | 1 天 |
| **P2** | RNG SplitMix64 展开、流名哈希换 FNV64、显式端序;`SanitizeSystem` 改 tick 取模;MovementExecutor 状态纳入 save 权威;`SpawnItem` 走 `_posIndex` + 日志守卫;xUnit 迁移;根目录清理;JSON 栈统一 | — | 合计 1–2 周 |
| **冻结** | 暂停两类工作直至 P0/P1 完成:① 继续拆 partial 的架构批次;② 一切性能优化(OPTIMIZATION_SUGGESTION 里的项大多会被 P1-1/P1-2 改写前提) | — | — |

**验收标准建议**:D1 的 CI 在 Linux 与 Windows 上、各连续 20 次运行、2000 tick 全节 hash 逐一相等——在此绿灯之前,README 与文档中的"deterministic"措辞建议一律降级为"determinism-targeted"。

---

## 7. 结语

用一次面试评语式的总结:**过程成熟度 A-,地基正确性 C,产品完成度 D+,但方向感和自我文档化能力罕见地好。** 这个代码库最危险的不是它写错的地方,而是它"写对了文档、写对了规则、甚至写对了检查规则的测试,唯独实现在最关键的几处走了捷径"——墙钟预算、名存实亡的缓存失效、被注释掩盖的撕裂读取,全都藏在正确叙事的阴影里。好消息是:上述 P0/P1 全部是**局部、机械、低风险**的修复(合计约 2–3 周),而项目已经拥有把修复固化下来所需的一切基础设施(hash builder、replay record、边界测试框架)。先把"确定性"从口号变成 CI 里的绿灯,这个项目就配得上它文档里描绘的那个野心。

---
*审计执行细节:clone 于 2026-07-07;`dotnet build HumanFortress.sln`(.NET 8.0)0 警告 0 错误;`HumanFortress.App.Tests.dll` 全部通过,exit code 0。所有 [已验证] 结论均可由文中给出的文件路径/行号复核。*
