# 新对话快速启动指南

## 🚀 立即开始前必读

你正在参与一个**矮人要塞风格**的复杂游戏项目。这个项目有精心设计的架构，你的任务是**增强**而非**重写**。

### ⚠️ 三大禁忌

1. **禁止猜测** - 不确定时必须先搜索/阅读代码
2. **禁止重写** - 已有功能必须复用，不要造轮子
3. **禁止简化** - 保持现有架构复杂度

### ✅ 黄金工作流

```
接到任务
  → 读 SPEC 文档 (docs/*.md)
  → 搜索现有代码 (Grep/Glob)
  → 阅读相关实现 (Read)
  → 与用户讨论方案
  → 小步修改 + 频繁编译
```

---

## 📁 项目结构速查

```
src/
  HumanFortress.Core/         # 核心类型、内容注册、随机数
  HumanFortress.Simulation/   # 世界、chunk、物品、生物
  HumanFortress.Navigation/   # 寻路
  HumanFortress.App/          # UI、渲染

content/registries/           # JSON 配置 (材料、tuning)
data/core/                    # 游戏数据 (items, constructions)
docs/                         # 设计文档 (SPEC)
```

---

## 🔍 必读文档（按优先级）

### 1. CHUNK_AND_DATA_LAYOUT.md ⭐⭐⭐
- Chunk = 32×32 固定
- Layer 系统: L0(地形) L2(家具) L5(物品)
- 脏传播: L0/L2 编辑 → tile + 6 邻居
- FurnitureCell = blocker + passables[]

```bash
Read("docs/CHUNK_AND_DATA_LAYOUT.md")
```

### 2. UPDATE_ORDER.md ⭐⭐⭐
- ITick 接口: ReadTick/WriteTick 分离
- ApplyCommands 阶段: 写 L0/L2/L7
- 确定性: 每阶段独立 RNG 流
- Chunk 并行执行

```bash
Read("docs/UPDATE_ORDER.md")
```

### 3. 相关 SPEC 文档
```bash
Read("docs/MATERIALS_SPEC.md")  # 材料系统
Read("docs/ITEMS_SPEC.md")      # 物品系统
Read("docs/PLACEABLE_SPEC.md")  # 可放置物
```

---

## 🛠️ 核心模式速查

### 模式 1: 确定性系统

❌ **错误**：
```csharp
var guid = Guid.NewGuid(); // 非确定性！
```

✅ **正确**：
```csharp
// 先搜索
Grep("DeterministicGuid")

// 使用现有工具
var guid = DeterministicGuidGenerator.GenerateFromPosition(tickSeed, x, y, z);
```

### 模式 2: Tuning 参数

❌ **错误**：
```csharp
int bonus = 1; // 硬编码
```

✅ **正确**：
```csharp
// 搜索现有 Tuning
Grep("class.*Tuning")

// 参考现有模式
Read("src/HumanFortress.Navigation/NavigationTuning.cs")

// 使用 tuning
tuning ??= PlaceableTuning.Default;
int bonus = tuning.BeautyPerTier;
```

### 模式 3: Layer 集成

**关键概念**：
- PlaceableInstance = 权威数据
- FurnitureRef = 轻量引用 (GUID)
- FurnitureCell = 派生缓存

✅ **正确流程**：
```csharp
// 1. 添加 placeable
placeableData.AddPlaceable(index, placeable);

// 2. 同步到 FurnitureCell
placeableData.SyncToFurnitureCell(chunk, placeable, tick);

// 3. 触发脏传播
chunk.BumpConnectivityVersion();
```

---

## 🔧 常用命令速查

### 搜索代码
```bash
# 找类
Grep("class ClassName")

# 找方法（含上下文）
Grep("MethodName", output_mode="content", "-C=5")

# 找文件
Glob("**/*placeable*.cs")

# 找 TODO
Grep("TODO:|FIXME:")
```

### 编译项目
```bash
# 单项目编译（快）
dotnet build src/HumanFortress.Core/HumanFortress.Core.csproj

# 完整编译
dotnet build src/HumanFortress.App/HumanFortress.App.csproj

# ❌ 不要编译 .sln（已损坏）
```

---

## 📋 工作检查清单

开始编码前：
- [ ] 已读相关 SPEC 文档
- [ ] 已搜索现有实现 (Grep)
- [ ] 已阅读相关代码 (Read)
- [ ] 已与用户讨论方案

编码时：
- [ ] 复用现有类/方法
- [ ] 使用 DeterministicRng（不用 Random）
- [ ] 保持代码风格一致
- [ ] 小步修改 + 频繁编译

完成后：
- [ ] 编译成功（无错误）
- [ ] 警告仅为 CS0618/CA1822 等已知类型
- [ ] 文件复制到构建输出（如需要）

---

## 🆘 遇到问题时

### 问题：找不到类型
```bash
# 1. 搜索类定义
Grep("class TypeName")

# 2. 检查命名空间
# 可能有同名 namespace 和 class！
# 使用 using alias 解决
```

### 问题：不知道如何实现功能 X
```bash
# 1. 搜索类似功能
Grep("关键词", output_mode="files_with_matches")

# 2. 阅读找到的实现
Read("找到的文件.cs")

# 3. 复制模式并修改
```

### 问题：编译错误
```bash
# 1. 读错误信息（完整）
# 2. 检查命名空间/using
# 3. 搜索错误的类型/方法
# 4. 与用户讨论
```

---

## 💡 记住

> **理解优先，编码其次**
> **搜索现有，避免重造**
> **小步前进，频繁测试**
> **不确定时，先问用户**

详细指南：`.claude/continuation_prompt.md`

---

## 🎯 现在开始

告诉用户你的任务，然后：

1. 列出需要阅读的 SPEC 文档
2. 搜索相关现有代码
3. 阅读并理解现有实现
4. 提出修改方案与用户讨论
5. 获得确认后开始编码

**Good luck! 慢即是快，理解即是力量。** 🚀
