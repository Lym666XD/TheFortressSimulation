# Creature & Item Tracking System - Implementation Summary

**Status**: Core Implementation Complete ✅
**Date**: 2025-09-30
**Version**: 1.0 - Minimum Viable Product

---

## ✅ 已完成的功能

### 1. **CreatureManager & ItemManager** (完整实现)
- ✅ Data-driven加载系统
- ✅ 线程安全设计
- ✅ 错误恢复机制
- ✅ Spawn功能集成
- ✅ 查询接口完整

### 2. **Debug菜单修复** (3个Tab)
- ✅ **Tab 0: Status** - 独立的状态页面(地图信息/相机/zoom)
- ✅ **Tab 1: Creatures** - 生物spawn(D/H/G/E/O选择)
- ✅ **Tab 2: Items** - 物品spawn(1-5选择)
- ✅ 点击地图spawn功能连接到Manager
- ✅ Toast提示和日志记录
- ✅ 数据路径自动搜索修复

### 3. **F1 Creatures Drawer** (生物追踪)
- ✅ **Tab 0: All Creatures** - 显示所有已spawn生物
  - 格式: `Name | Pos (x,y,z) | [Status]`
  - 统计: Total/Alive/Dead计数
  - 选中高亮(黄色背景)
- ✅ **Tab 1: Animals** - 占位页面"Coming soon"
- ✅ Tab 2: Settings - 占位
- ✅ 空状态友好提示

### 4. **F2 Stock/Items Drawer** (物品追踪)
- ✅ **Tab 0: Items** - 显示地图上所有物品
  - Kind过滤: all/resource/weapon/armor/tool/container/consumable
  - 格式: `Name xQty | @ (x,y,z)`
  - 统计: Total items / Total units
  - 选中高亮(黄色背景)
- ✅ **Tab 1: Stockpiles** - 保持原有功能
- ✅ Tab 2: Trade - 占位

---

## 🎮 用户操作流程

### **Debug Menu (F12)**
```
1. 按F12打开debug菜单
2. 按TAB或0/1/2切换tab
3. Tab 0: 查看状态信息
4. Tab 1: 选择生物(D/H/G/E/O) → 点击地图spawn
5. Tab 2: 选择物品(1-5) → 点击地图spawn
6. ESC关闭菜单
```

### **Creature Tracking (F1)**
```
1. 按F1打开Creatures drawer
2. Tab 0显示所有生物列表
3. (未来)点击生物行 → 相机跳转+详情面板
4. Tab 1: Animals(占位)
5. TAB切换tab,ESC关闭
```

### **Item Tracking (F2)**
```
1. 按F2打开Stock/Items drawer
2. Tab 0显示所有物品
3. 点击kind过滤(resource/weapon等) → 过滤列表
4. (未来)点击物品行 → 相机跳转+详情面板
5. Tab 1: Stockpiles(已有)
6. TAB切换tab,ESC关闭
```

---

## 🔧 技术实现细节

### **数据流**
```
Startup:
  World.Creatures.LoadDefinitions(data/core/creatures/)
  World.Items.LoadDefinitions(data/core/items/)
    ↓
Spawn:
  Debug Menu选择 → OnMapLeftClickedLocal
    → World.Creatures.SpawnCreature()
    → CreatureInstance创建
    → 日志记录
    ↓
Display:
  F1/F2 Drawer打开
    → UiRenderer.DrawCreaturesTab() / DrawItemsTab()
      → World.Creatures.GetAllInstances()
      → 渲染列表+统计+过滤
```

### **代码位置**
- `src/HumanFortress.Simulation/Creatures/` - CreatureManager系统
- `src/HumanFortress.Simulation/Items/` - ItemManager系统
- `src/HumanFortress.App/UI/UiRenderer.cs` - DrawCreaturesTab/DrawItemsTab
- `src/HumanFortress.App/UI/UiStore.cs` - SelectedCreatureGuid/ItemKindFilter状态
- `src/HumanFortress.App/States/FortressState.cs` - OnMapLeftClickedLocal spawn逻辑

---

## 🚧 待实现功能(未来迭代)

### **高优先级**
1. **点击跳转** - 点击生物/物品行时:
   - 相机跳转到目标位置(需要添加SetCamera方法)
   - 选中目标(SelectedCreatureGuid/ItemGuid设置)
   - 可选: Z层自动切换

2. **详情面板** - 选中后右侧显示:
   - 生物: HP/MaxHP, Stats, Skills, Inventory
   - 物品: Quality, Condition, Material, Value

3. **地图高亮** - 选中实体在地图上高亮显示
   - 用不同颜色/闪烁glyph标记

### **中优先级**
4. **过滤功能**:
   - F1: 文本搜索框(按name)
   - F1: 状态过滤(Alive/Dead/Idle/Moving)
   - F2: Kind过滤已完成,可添加文本搜索

5. **排序选项**:
   - 按距离排序(最近的在前)
   - 按状态排序
   - 按类型排序

6. **滚动支持**:
   - 当列表超过20项时支持上下滚动
   - 显示滚动条位置指示

### **低优先级**
7. **Animals Tab实现**:
   - 定义animal标签(tags: ["animal"])
   - 从Creatures过滤出animals
   - 显示驯化状态/年龄等

8. **批量操作**:
   - Shift+点击多选
   - 批量删除/移动

---

## 📊 当前限制

1. **显示限制**: 每个列表最多显示20项(硬编码)
2. **没有滚动**: 超过20项只显示"... and X more"
3. **没有点击交互**: 点击生物/物品行暂无响应
4. **没有详情面板**: 选中后只有背景高亮,无详细信息
5. **没有地图高亮**: 选中的生物/物品在地图上无标记
6. **没有持久化**: Manager状态不会保存到存档

---

## 🧪 测试步骤

### **基础功能测试**
```
1. 启动游戏 → 进入Fortress Play
2. 检查log: 应该看到"Loaded X creatures, Y items"
3. 按F12 → Tab 0查看Status(应该正常显示)
4. Tab 1 → 按D选择Dwarf → 点击地图 → 检查Toast和log
5. Tab 2 → 按1选择Stone → 点击地图 → 检查Toast和log
6. 按F1 → 应该看到spawned的生物列表
7. 按F2 → Tab 0应该看到spawned的物品列表
8. 点击kind过滤 → 验证列表更新
```

### **错误处理测试**
```
1. 点击墙壁spawn → 应该显示"Spawn failed"
2. 选择不存在的生物ID → 应该在log显示错误
3. data/core目录不存在 → 应该显示WARNING但不崩溃
```

---

## 🐛 已知问题

1. **Kind过滤点击**: 暂时没有实现kind过滤的点击处理,需要用键盘切换
2. **选中持久化**: 切换tab后选中状态会丢失
3. **Z层不同步**: 显示的生物可能在不同Z层,但列表不区分

---

## 📝 建议的下一步

**与你讨论后决定优先级,建议顺序**:
1. ✅ 先测试当前实现,确认debug menu和spawn功能正常
2. 🔄 添加点击跳转功能(相机移动到目标位置)
3. 🔄 添加简单的详情面板(右侧小窗口)
4. 🔄 添加地图高亮标记(选中的实体用特殊符号)
5. ⏸️  Kind过滤点击处理
6. ⏸️  滚动功能
7. ⏸️  Animals tab实现

---

## 💡 扩展建议(未来可能性)

- **命令系统**: 右键生物/物品弹出命令菜单(删除/移动/检查)
- **分组显示**: 按种族/faction分组显示生物
- **图表统计**: 饼图显示物品分布
- **导出功能**: 导出生物/物品列表到CSV
- **实时更新**: 生物状态变化时自动刷新列表

---

**编译状态**: ✅ 0 Errors, 4 Warnings (all cosmetic)
**文档**: CREATURE_ITEM_MANAGER.md, TRACKING_SYSTEM_SUMMARY.md
**准备测试**: 是