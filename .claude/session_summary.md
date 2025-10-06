# 会话工作总结

## 完成时间
2025-10-05

## 完成的任务

### 1. P0 架构修正（关键）✅

#### 1.1 确定性 GUID 生成
**问题**：`PlaceableInstance` 使用 `Guid.NewGuid()` 破坏确定性重放

**解决**：
- 创建 `DeterministicGuidGenerator.cs`
  - 使用 `DeterministicRng` 生成 GUID
  - 基于位置和 tick seed：`seed = tickSeed ^ HashPosition(x,y,z)`
  - FNV-1a 哈希算法确保良好分布
- 更新 `PlaceableInstance.CreateFromItem()` 和 `CreateFromConstruction()`
  - 添加 `ulong tickSeed` 参数
  - 替换 `Guid.NewGuid()` 为确定性生成

**文件**：
- `src/HumanFortress.Core/Random/DeterministicGuidGenerator.cs`
- `src/HumanFortress.Simulation/Placeables/PlaceableInstance.cs:156-159`

---

#### 1.2 PlaceableData 与 FurnitureCell 集成
**问题**：新的 PlaceableData 系统与现有 FurnitureCell 冲突

**解决方案**：集成而非替代
- **PlaceableInstance** = 权威数据（authoritative source）
- **FurnitureRef** = 轻量引用（GUID 指向 PlaceableInstance）
- **FurnitureCell** = 派生缓存（在 RebuildDerived 阶段重建）

**实现**：
- 扩展 `FurnitureRef` 结构支持 GUID 引用（保持向后兼容）
  ```csharp
  public readonly Guid PlaceableGuid;  // 新字段
  public bool IsPlaceable => PlaceableGuid != Guid.Empty;
  ```
- `ChunkPlaceableData.SyncToFurnitureCell()` 方法
  - 遍历 footprint 所有单元格
  - 创建 `FurnitureRef(placeable.Guid)` 引用
  - 调用 `chunk.PlaceFurniture()` 写入 L2

**文件**：
- `src/HumanFortress.Simulation/World/Chunk.cs:250-277` (FurnitureRef)
- `src/HumanFortress.Simulation/Placeables/ChunkPlaceableData.cs:103-139` (SyncToFurnitureCell)

---

#### 1.3 脏传播（Dirty Propagation）
**问题**：PlaceableData 操作不触发缓存失效

**解决**：
- 添加 `Chunk.BumpConnectivityVersion()` 方法
  - 递增 `ConnectivityVersion` 使 NavMask/OpacMask 失效
- 添加 `Chunk.MarkTileDirty()` 方法
  - 标记 tile + 邻居需要重建
  - 更新 `LastModifiedTick`

**使用**：
```csharp
placeableData.SyncToFurnitureCell(chunk, placeable, tick);
chunk.BumpConnectivityVersion();  // 失效缓存
chunk.MarkTileDirty(localIndex, tick);  // 标记脏
```

**文件**：
- `src/HumanFortress.Simulation/World/Chunk.cs:201-226`

---

#### 1.4 跨区块碰撞检测
**问题**：仅检查锚点单元格，5×5 工坊可能重叠

**解决**：
- 创建 `PlaceableManager.cs` 处理跨区块操作
- `CheckCollision()` 方法验证完整 footprint
  - 遍历所有 W×D 单元格
  - 检查每个单元格的占用状态
  - 检查地形可行走性
  - 跨多个 chunk 验证

**文件**：
- `src/HumanFortress.Simulation/Placeables/PlaceableManager.cs:19-72`

---

#### 1.5 两阶段跨区块放置协议
**问题**：5×5 工坊跨越 4 个 chunk（32×32 边界），chunk 并行写冲突

**解决**：
- `PlaceableManager.PlacePlaceable()` 实现两阶段协议
  - **Phase 1 (ReadTick)**：收集所有受影响的 chunk
  - **Phase 2 (WriteTick)**：
    - 主 chunk：写入 PlaceableInstance + 同步 FurnitureCell
    - 次 chunk：添加 ExternalRef + 同步 FurnitureCell
    - 所有 chunk：BumpConnectivityVersion

**文件**：
- `src/HumanFortress.Simulation/Placeables/PlaceableManager.cs:78-160`

---

### 2. Tuning 参数抽离 ✅

#### 2.1 创建 PlaceableTuning 类
**目标**：消除硬编码参数，支持运行时配置

**实现**：
- 参考 `NavigationTuning.cs` 模式
- 创建 `PlaceableTuning.cs` 包含所有可调参数
- 实现 `LoadFromContent()` 从 JSON 加载
- 使用 `ContentRegistry.Instance.GetTuning<JObject>()` API

**参数类别**：
- Quality：beauty/comfort per tier
- Durability：default HP, material multipliers, condition thresholds
- Installation：time costs, recovery rate
- Construction：quality rules, XP gain
- Doors：default states, costs
- Collision：validation flags

**文件**：
- `src/HumanFortress.Core/Content/Registry/PlaceableTuning.cs`

---

#### 2.2 重构 PlaceableInstance 使用 Tuning
**修改**：
- `CreateFromItem()` 添加 `PlaceableTuning?` 参数
  - 质量效果：`Beauty = base + quality * tuning.BeautyPerTier`
  - HP 计算：`HP = volume * tuning.HPPerML * materialMultiplier`
  - 材质类别提取：`"core_mat_metal_iron" → "metal"`

- `CreateFromConstruction()` 添加 `PlaceableTuning?` 参数
  - HP 计算：`HP = materialCount * tuning.DefaultMaxHP`

- `CreateItemFromPlaceable()` 添加 `PlaceableTuning?` 参数
  - 使用 `tuning.ConditionThresholds` 动态计算 ConditionState

- `ComputeConditionState()` 改为动态阈值
  - 排序 thresholds，匹配最高满足的阈值

**文件**：
- `src/HumanFortress.Simulation/Placeables/PlaceableInstance.cs:150-308`

---

#### 2.3 创建 tuning.placeable.json
**位置**：`content/registries/tuning.placeable.json`（不是 `data/core/tuning/`）

**结构**：
```json
{
  "quality": { "beauty_per_tier": 1, "comfort_per_tier": 1 },
  "durability": {
    "default_max_hp": 100,
    "hp_per_volume_ml": 0.001,
    "material_hp_multiplier": { "stone": 2.0, "metal": 3.0, "wood": 1.0 },
    "condition_thresholds": { "pristine": 1.0, "good": 0.9, ... }
  },
  "installation": { ... },
  "construction": { ... },
  "doors": { ... },
  "collision": { ... }
}
```

**自动部署**：构建时复制到 `bin/.../content/registries/`

**文件**：
- `content/registries/tuning.placeable.json`

---

### 3. 文档与知识传递 ✅

#### 3.1 架构理解指南
创建详细的架构理解文档，帮助后续 AI 对话快速上手

**内容**：
- 核心原则（禁止猜测、重写、简化）
- 项目结构详解
- 必读 SPEC 文档清单
- 代码模式速查（确定性、Tuning、Layer 集成）
- 搜索策略与工作流
- 常见陷阱与解决方案
- 编译测试指南

**文件**：
- `.claude/continuation_prompt.md` (详细版, 5000+ 字)
- `.claude/quick_start.md` (快速版, 1500 字)

---

#### 3.2 会话总结
本文档，记录所有完成的工作和关键决策

**文件**：
- `.claude/session_summary.md`

---

## 技术决策记录

### 决策 1：集成 vs 替代 FurnitureCell
**选择**：集成（PlaceableInstance 为 authoritative，FurnitureCell 为 derived）

**理由**：
- FurnitureCell 已深度集成到现有系统
- 避免大规模重构现有代码
- 符合 "derived cache" 模式（如 NavMask）

---

### 决策 2：效果计算方案
**选择**：方案 B - 存储计算后的值

**理由**：
- 效果值频繁查询（美观度计算、UI 显示）
- 很少修改（仅安装/卸载时）
- 避免运行时计算开销

---

### 决策 3：Tuning 参数位置
**选择**：`content/registries/` 而非 `data/core/tuning/`

**理由**：
- 与其他 tuning 文件（navigation, mining 等）保持一致
- ContentRegistry 已配置为从 `content/registries/` 加载
- 自动部署到构建输出

---

### 决策 4：HP 计算公式
**Installable**：`HP = volume * HPPerML * materialMultiplier`

**Construction**：`HP = materialCount * defaultMaxHP`

**理由**：
- Installable 有明确的物品体积和材质
- Construction 体积不明确，使用材料数量作为代理

---

## 遗留问题（待后续处理）

### P1 问题（重要）
1. **FurnitureCell 移除操作**
   - `UnsyncFromFurnitureCell()` 仅为占位符
   - 需要 `Chunk.RemoveFurniture()` 方法

2. **预留系统集成**
   - 多个 worker 可能同时预留同一位置
   - 需要与 `ReservationManager` 集成

3. **PassabilityMode 实际读取**
   - `IsBlockingPassability()` 目前硬编码返回 true
   - 需要读取 `PlaceableProfile.Passability`

### P2 问题（优化）
1. **HP 计算精细化**
   - 当前简化算法
   - 未来可基于材料密度、结构复杂度

2. **质量修正器可配置**
   - 不同效果类型可能需要不同修正器
   - 例如：beauty_per_tier vs comfort_per_tier 可能不同

---

## 构建状态

### 编译测试 ✅
- **HumanFortress.Core**: ✅ 成功（0 错误，0 警告）
- **HumanFortress.Simulation**: ✅ 成功（0 错误，34 警告 - 均为已知类型）
- **HumanFortress.App**: ✅ 成功（0 错误，28 警告 - 均为已知类型）

### 已知警告类型（可忽略）
- `CS0618`: Obsolete 成员（计划迁移）
- `CS8602/CS8604`: Nullable 引用警告
- `CA1822`: 可标记为 static
- `CA1869`: JsonSerializerOptions 缓存

---

## 关键文件清单

### 新增文件
```
src/HumanFortress.Core/Random/
  DeterministicGuidGenerator.cs          # 确定性 GUID 生成

src/HumanFortress.Core/Content/Registry/
  PlaceableTuning.cs                     # Tuning 参数类

src/HumanFortress.Simulation/Placeables/
  PlaceableManager.cs                    # 跨区块管理器

content/registries/
  tuning.placeable.json                  # Tuning 配置

.claude/
  continuation_prompt.md                 # 详细架构指南
  quick_start.md                         # 快速启动指南
  session_summary.md                     # 本文档
```

### 修改文件
```
src/HumanFortress.Simulation/Placeables/
  PlaceableInstance.cs                   # 添加 tuning 参数、确定性 GUID
  ChunkPlaceableData.cs                  # 添加 SyncToFurnitureCell

src/HumanFortress.Simulation/World/
  Chunk.cs                               # 扩展 FurnitureRef、添加脏传播方法
```

---

## 下一步建议

### 立即可做
1. **实现 ConstructionDefinition 加载**
   - 创建 `data/core/constructions/*.json`
   - 参考 `items/*.json` 结构
   - 在 `ContentRegistry` 添加加载逻辑

2. **实现安装/卸载命令**
   - 参考现有 Command 模式
   - 使用 `PlaceableManager.CheckCollision()` 验证
   - 使用 `PlaceableManager.PlacePlaceable()` 执行

3. **集成预留系统**
   - 在 `PlaceableManager.CheckCollision()` 检查预留
   - 在放置前获取预留 token

### 需要设计讨论
1. **门状态机制**
   - DoorState 何时更新
   - 寻路系统如何查询门状态

2. **建造工作流**
   - 建造命令 → 工作分配 → 进度跟踪
   - 材料消耗时机

3. **卸载/拆除恢复**
   - 材料恢复率应用
   - 损坏物品的处理

---

## 给下一个对话的话

你现在掌握了这个项目的关键架构知识。记住：

**理解优先，编码其次**

每次开始新任务前：
1. 阅读相关 SPEC 文档
2. 搜索现有实现
3. 理解现有模式
4. 与用户讨论方案
5. 小步修改 + 频繁编译

这个项目的架构复杂但有序，你的任务是增强它，而非重建它。

祝好运！🚀
