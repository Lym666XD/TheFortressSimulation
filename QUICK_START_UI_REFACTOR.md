# UI重构 - 快速开始指南

## ⚡ 5分钟集成新UI系统

### Step 1: 打开文件
打开 `src/HumanFortress.App/States/FortressState.cs`

### Step 2: 找到Line 175-183
查找这段代码:
```csharp
_uiSurface = new UiOverlaySurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
// Overlay actively handles clicks for dock/quick/debug
_uiSurface.UseMouse = true;
_uiSurface.UseKeyboard = false;  // ← 改这里
_uiSurface.FocusOnMouseClick = false;
_uiSurface.LeftClickedLocal += OnOverlayLeftClickedLocal;  // ← 注释掉
_uiSurface.RightClickedLocal += OnOverlayRightClickedLocal;  // ← 注释掉
```

### Step 3: 应用更改
```csharp
_uiSurface = new UiOverlaySurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
// Overlay actively handles clicks for dock/quick/debug
_uiSurface.UseMouse = true;
_uiSurface.UseKeyboard = true;  // ✅ 改为 true
_uiSurface.FocusOnMouseClick = false;

// ✅ 添加新的InputHandlerComponent
var uiStateManager = new UI.UIStateManager(_ui);
var inputHandler = new UI.Components.InputHandlerComponent(
    uiStateManager,
    GameHost.Instance.ScreenCellsX,
    GameHost.Instance.ScreenCellsY
);
_uiSurface.SadComponents.Add(inputHandler);
Logger.Log($"[INIT] Added InputHandlerComponent");

// ✅ 注释掉旧handlers
// _uiSurface.LeftClickedLocal += OnOverlayLeftClickedLocal;
// _uiSurface.RightClickedLocal += OnOverlayRightClickedLocal;
_uiSurface.MouseMovedLocal += OnOverlayMouseMovedLocal; // 保留这个
Logger.Log($"[INIT] UiOverlay size={_uiSurface.Surface.Width}x{_uiSurface.Surface.Height}");
```

### Step 4: 编译
```bash
./build.bat
```

### Step 5: 测试
运行游戏，测试:
- ✅ F1-F8键打开drawer
- ✅ 鼠标点击F1-F8按钮
- ✅ Z/X/C/V键打开quick menu
- ✅ 鼠标点击Z/X/C/V按钮
- ✅ 右键导航返回

### ✅ 完成!

查看日志应该看到:
```
[INIT] Added InputHandlerComponent
[InputHandler] Dock button 2 -> Work
[UIStateManager] ToggleDrawer: Work
```

---

## 🔧 如果编译失败

### 错误: CS0103 'UIStateManager' 不存在

**解决**: 在FortressState.cs顶部添加:
```csharp
using HumanFortress.App.UI;
using HumanFortress.App.UI.Components;
```

---

## 📋 测试通过后

### 可选: 删除旧代码

这些方法现在是死代码,可以安全删除:

1. `OnOverlayLeftClickedLocal` (约line 435, ~70行)
2. `OnOverlayRightClickedLocal` (约line 1809, ~75行)
3. `HandleDockClicksScreen` (约line 1526, ~90行)
4. Map-relative click handler (约line 1378, ~40行)

**总共可删除**: ~275 lines of legacy code

---

## 🎯 新架构的优势

| Before | After |
|--------|-------|
| 3个重复click handlers | 1个InputHandlerComponent |
| 手动位置计算(不同步) | ButtonLayoutCalculator (单一来源) |
| F3/F4错位 | ✅ 修复 |
| ZXCV不可点击 | ✅ 修复 |
| 无右键支持 | ✅ 完整支持 |
| 2510行God Object | 清晰分离的组件 |

---

## 📚 详细文档

- **UI_REFACTOR_SUMMARY_CN.md** - 完整总结(中文)
- **APPLY_INPUT_HANDLER_PATCH.md** - 详细集成步骤
- **UI_REFACTOR_PROGRESS.md** - 技术进度报告

---

**任何问题? 检查 `fortress_debug.log` 或询问我!** 🚀
