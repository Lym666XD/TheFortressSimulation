# InputHandlerComponent Integration Patch

## 问题
由于文件锁定，无法自动编辑 `FortressState.cs`。请手动应用以下更改。

## 文件: src/HumanFortress.App/States/FortressState.cs

### 修改位置: Line 175-183

**查找这段代码:**
```csharp
                _uiSurface = new UiOverlaySurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
                // Overlay actively handles clicks for dock/quick/debug
                _uiSurface.UseMouse = true;
                _uiSurface.UseKeyboard = false;
                _uiSurface.FocusOnMouseClick = false; // don't steal focus
                _uiSurface.LeftClickedLocal += OnOverlayLeftClickedLocal;
                _uiSurface.RightClickedLocal += OnOverlayRightClickedLocal;
                _uiSurface.MouseMovedLocal += OnOverlayMouseMovedLocal;
                Logger.Log($"[INIT] UiOverlay size={_uiSurface.Surface.Width}x{_uiSurface.Surface.Height}");
```

**替换为:**
```csharp
                _uiSurface = new UiOverlaySurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
                // Overlay actively handles clicks for dock/quick/debug
                _uiSurface.UseMouse = true;
                _uiSurface.UseKeyboard = true; // Enable keyboard for InputHandlerComponent
                _uiSurface.FocusOnMouseClick = false; // don't steal focus

                // NEW: Use InputHandlerComponent for all UI input (replaces old click handlers)
                var uiStateManager = new UI.UIStateManager(_ui);
                var inputHandler = new UI.Components.InputHandlerComponent(
                    uiStateManager,
                    GameHost.Instance.ScreenCellsX,
                    GameHost.Instance.ScreenCellsY
                );
                _uiSurface.SadComponents.Add(inputHandler);
                Logger.Log($"[INIT] Added InputHandlerComponent to UiOverlay");

                // OLD: Legacy click handlers (commented out for testing)
                // _uiSurface.LeftClickedLocal += OnOverlayLeftClickedLocal;
                // _uiSurface.RightClickedLocal += OnOverlayRightClickedLocal;
                _uiSurface.MouseMovedLocal += OnOverlayMouseMovedLocal; // Still needed for hover
                Logger.Log($"[INIT] UiOverlay size={_uiSurface.Surface.Width}x{_uiSurface.Surface.Height}");
```

## 说明

### 更改内容:
1. **启用键盘输入**: `UseKeyboard = true` (原来是false)
2. **添加InputHandlerComponent**: 创建UIStateManager和InputHandlerComponent，添加到_uiSurface
3. **注释掉旧的事件处理**: `LeftClickedLocal` 和 `RightClickedLocal` 被注释
4. **保留MouseMovedLocal**: 仍然需要用于hover功能

### 为什么这样做:
- InputHandlerComponent是新的统一输入处理器
- 它使用ButtonLayoutCalculator进行点击测试(消除重复代码)
- 它将输入转换为UI Commands(更清晰的架构)
- 旧的OnOverlayLeftClickedLocal和OnOverlayRightClickedLocal将不再被调用

### 测试后:
如果InputHandlerComponent工作正常，可以完全删除以下方法:
- `OnOverlayLeftClickedLocal` (line ~435)
- `OnOverlayRightClickedLocal` (line ~1809)
- 以及所有死代码的click handlers

## 应用此patch后的下一步

1. 保存文件
2. 编译: `dotnet build src/HumanFortress.App/HumanFortress.App.csproj`
3. 运行游戏测试:
   - F1-F8按钮是否正常打开drawer?
   - Z/X/C/V按钮是否正常打开quick menu?
   - 鼠标点击F1-F8是否work?
   - 鼠标点击Z/X/C/V是否work?
   - 右键是否正常导航返回?
4. 检查日志: `publish/HumanFortress.App/fortress_debug.log`
   - 应该看到`[InputHandler]`日志而不是`[CLICK-OVERLAY]`

## 如果遇到编译错误

可能的错误:
```
error CS0103: The name 'UIStateManager' does not exist in the current context
```

**解决方案**: 在文件顶部添加using:
```csharp
using HumanFortress.App.UI;
using HumanFortress.App.UI.Components;
```

## 回滚方案

如果新系统不工作，取消注释这两行:
```csharp
_uiSurface.LeftClickedLocal += OnOverlayLeftClickedLocal;
_uiSurface.RightClickedLocal += OnOverlayRightClickedLocal;
```

并注释掉InputHandlerComponent相关的代码。
