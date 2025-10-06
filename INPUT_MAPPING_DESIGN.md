# Input Mapping System Design - 完全解耦的输入架构

## 当前问题

### 硬编码的键位绑定
```csharp
// InputHandlerComponent.cs - 硬编码!
if (keyboard.IsKeyPressed(Keys.F1))
    new ToggleDrawerCommand(DrawerId.Creature).Execute(_uiStateManager);
```

**问题**：
- ❌ 无法修改键位绑定（需要改代码）
- ❌ 无法用户自定义
- ❌ 无法支持多种输入设备（手柄、触摸等）
- ❌ 无法保存用户配置

---

## 🎯 新架构设计：InputMapping System

### 核心概念

```
用户按键 → InputEvent → InputMapper → ActionName → CommandFactory → IUICommand → Execute
```

#### 1. **InputEvent** - 输入事件抽象
所有输入设备产生统一的InputEvent
```csharp
public abstract class InputEvent
{
    public string EventId { get; }  // "keyboard.F1", "gamepad.A", "mouse.left"
}

public class KeyboardInputEvent : InputEvent
{
    public Keys Key { get; }
    public KeyboardInputEvent(Keys key)
        => EventId = $"keyboard.{key}";
}

public class GamepadInputEvent : InputEvent
{
    public GamepadButton Button { get; }
    public GamepadInputEvent(GamepadButton btn)
        => EventId = $"gamepad.{btn}";
}

public class MouseInputEvent : InputEvent
{
    public MouseButton Button { get; }
    public Point Position { get; }
}
```

#### 2. **InputMapper** - 输入映射器
将InputEvent映射到ActionName
```csharp
public class InputMapper
{
    // 键位绑定表：EventId → ActionName
    private readonly Dictionary<string, string> _bindings = new();

    public InputMapper()
    {
        // 默认绑定（可从配置文件加载）
        LoadDefaultBindings();
    }

    private void LoadDefaultBindings()
    {
        // 键盘绑定
        _bindings["keyboard.F1"] = "ui.toggle_drawer.creature";
        _bindings["keyboard.F2"] = "ui.toggle_drawer.stock";
        _bindings["keyboard.F3"] = "ui.toggle_drawer.work";
        _bindings["keyboard.Z"] = "ui.toggle_quick_menu.orders";
        _bindings["keyboard.Escape"] = "ui.cancel";

        // 手柄绑定
        _bindings["gamepad.Y"] = "ui.toggle_drawer.creature";  // Y键 = F1
        _bindings["gamepad.X"] = "ui.toggle_quick_menu.orders"; // X键 = Z
        _bindings["gamepad.B"] = "ui.cancel";                   // B键 = ESC

        // 鼠标绑定
        _bindings["mouse.right"] = "ui.cancel";  // 右键 = ESC
    }

    public string? MapToAction(InputEvent inputEvent)
    {
        return _bindings.TryGetValue(inputEvent.EventId, out var action)
            ? action
            : null;
    }

    // 用户自定义绑定
    public void SetBinding(string eventId, string actionName)
    {
        _bindings[eventId] = actionName;
    }

    // 保存到配置文件
    public void SaveToFile(string path)
    {
        var json = JsonSerializer.Serialize(_bindings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }

    // 从配置文件加载
    public void LoadFromFile(string path)
    {
        if (!File.Exists(path)) return;
        var json = File.ReadAllText(path);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (loaded != null)
        {
            _bindings.Clear();
            foreach (var kvp in loaded)
                _bindings[kvp.Key] = kvp.Value;
        }
    }
}
```

#### 3. **CommandFactory** - 命令工厂
将ActionName转换为IUICommand
```csharp
public class UICommandFactory
{
    private readonly UIStateManager _uiStateManager;

    public UICommandFactory(UIStateManager uiStateManager)
    {
        _uiStateManager = uiStateManager;
    }

    public IUICommand? CreateCommand(string actionName)
    {
        // 解析actionName: "ui.toggle_drawer.creature"
        var parts = actionName.Split('.');

        if (parts.Length < 2) return null;

        switch (parts[1])
        {
            case "toggle_drawer" when parts.Length == 3:
                var drawerId = ParseDrawerId(parts[2]);
                return new ToggleDrawerCommand(drawerId);

            case "toggle_quick_menu" when parts.Length == 3:
                var menuKind = ParseQuickMenuKind(parts[2]);
                return new ToggleQuickMenuCommand(menuKind);

            case "cancel":
                return new CancelCommand();

            case "navigate_back":
                return new NavigateBackCommand();

            case "toggle_help":
                return new ToggleHelpCommand();

            case "toggle_debug":
                return new ToggleDebugCommand();

            default:
                return null;
        }
    }

    private DrawerId ParseDrawerId(string name)
    {
        return name.ToLower() switch
        {
            "creature" => DrawerId.Creature,
            "stock" => DrawerId.Stock,
            "work" => DrawerId.Work,
            "placement" => DrawerId.PlacementManagement,
            "military" => DrawerId.Military,
            "country" => DrawerId.Country,
            "world" => DrawerId.World,
            "log" => DrawerId.Log,
            _ => DrawerId.None
        };
    }

    private QuickMenuKind ParseQuickMenuKind(string name)
    {
        return name.ToLower() switch
        {
            "orders" => QuickMenuKind.Orders,
            "zones" => QuickMenuKind.Zones,
            "build" => QuickMenuKind.Build,
            "stockpile" => QuickMenuKind.Stockpile,
            _ => QuickMenuKind.None
        };
    }
}
```

#### 4. **UnifiedInputHandler** - 统一输入处理器
替代当前的InputHandlerComponent
```csharp
public class UnifiedInputHandler : IComponent
{
    private readonly InputMapper _inputMapper;
    private readonly UICommandFactory _commandFactory;
    private readonly UIStateManager _uiStateManager;
    private readonly ButtonLayoutCalculator _buttonLayout;

    public UnifiedInputHandler(
        InputMapper inputMapper,
        UICommandFactory commandFactory,
        UIStateManager uiStateManager,
        int screenWidth,
        int screenHeight)
    {
        _inputMapper = inputMapper;
        _commandFactory = commandFactory;
        _uiStateManager = uiStateManager;
        _buttonLayout = new ButtonLayoutCalculator(screenWidth, screenHeight);
    }

    public void ProcessKeyboard(IScreenObject host, Keyboard keyboard, out bool handled)
    {
        handled = false;

        // 遍历所有按下的键，转换为InputEvent
        foreach (var key in keyboard.KeysPressed)
        {
            var inputEvent = new KeyboardInputEvent(key.Key);
            if (TryExecuteAction(inputEvent))
            {
                handled = true;
                break;
            }
        }
    }

    public void ProcessMouse(IScreenObject host, MouseScreenObjectState state, out bool handled)
    {
        handled = false;
        var localPos = state.SurfaceCellPosition;

        // 左键点击
        if (state.Mouse.LeftClicked)
        {
            // 优先检测UI按钮点击
            if (TryHandleUIButtonClick(localPos))
            {
                handled = true;
                return;
            }
        }

        // 右键点击
        if (state.Mouse.RightClicked)
        {
            var inputEvent = new MouseInputEvent
            {
                Button = MouseButton.Right,
                Position = localPos
            };

            // 如果有UI打开，消费右键
            if (_uiStateManager.HasOpenUI)
            {
                if (TryExecuteAction(inputEvent))
                {
                    handled = true;
                    return;
                }
            }
        }
    }

    // 尝试执行Action
    private bool TryExecuteAction(InputEvent inputEvent)
    {
        var actionName = _inputMapper.MapToAction(inputEvent);
        if (actionName == null) return false;

        var command = _commandFactory.CreateCommand(actionName);
        if (command == null) return false;

        command.Execute(_uiStateManager);
        Logger.Log($"[UnifiedInput] {inputEvent.EventId} → {actionName} → {command.CommandType}");
        return true;
    }

    // 处理UI按钮点击（F1-F8, ZXCV）
    private bool TryHandleUIButtonClick(Point localPos)
    {
        // 检测Dock按钮
        int? dockSlot = _buttonLayout.HitTestDockButtons(localPos);
        if (dockSlot.HasValue)
        {
            var actionName = $"ui.toggle_drawer.{GetDrawerName(dockSlot.Value)}";
            var command = _commandFactory.CreateCommand(actionName);
            command?.Execute(_uiStateManager);
            return true;
        }

        // 检测Quick按钮
        int? quickSlot = _buttonLayout.HitTestQuickButtons(localPos);
        if (quickSlot.HasValue)
        {
            var actionName = $"ui.toggle_quick_menu.{GetQuickMenuName(quickSlot.Value)}";
            var command = _commandFactory.CreateCommand(actionName);
            command?.Execute(_uiStateManager);
            return true;
        }

        return false;
    }

    private string GetDrawerName(int slot)
    {
        return slot switch
        {
            0 => "creature",
            1 => "stock",
            2 => "work",
            3 => "placement",
            4 => "military",
            5 => "country",
            6 => "world",
            7 => "log",
            _ => "none"
        };
    }

    private string GetQuickMenuName(int slot)
    {
        return slot switch
        {
            0 => "orders",
            1 => "zones",
            2 => "build",
            3 => "stockpile",
            _ => "none"
        };
    }

    // IComponent implementation
    public void OnAdded(IScreenObject host) { }
    public void OnRemoved(IScreenObject host) { }
    public void Render(IScreenObject host, TimeSpan delta) { }
    public void Update(IScreenObject host, TimeSpan delta) { }
    public uint SortOrder { get; set; } = 0;
    public bool IsUpdate => false;
    public bool IsRender => false;
    public bool IsMouse => true;
    public bool IsKeyboard => true;
}
```

---

## 📁 配置文件示例

### keybindings.json
```json
{
  "keyboard.F1": "ui.toggle_drawer.creature",
  "keyboard.F2": "ui.toggle_drawer.stock",
  "keyboard.F3": "ui.toggle_drawer.work",
  "keyboard.F4": "ui.toggle_drawer.placement",
  "keyboard.F5": "ui.toggle_drawer.military",
  "keyboard.F6": "ui.toggle_drawer.country",
  "keyboard.F7": "ui.toggle_drawer.world",
  "keyboard.F8": "ui.toggle_drawer.log",

  "keyboard.Z": "ui.toggle_quick_menu.orders",
  "keyboard.X": "ui.toggle_quick_menu.zones",
  "keyboard.C": "ui.toggle_quick_menu.build",
  "keyboard.V": "ui.toggle_quick_menu.stockpile",

  "keyboard.Escape": "ui.cancel",
  "keyboard.Back": "ui.navigate_back",
  "keyboard.F9": "ui.toggle_help",
  "keyboard.F10": "ui.toggle_debug",

  "gamepad.Y": "ui.toggle_drawer.creature",
  "gamepad.B": "ui.cancel",
  "gamepad.DPadLeft": "ui.toggle_quick_menu.orders",
  "gamepad.DPadRight": "ui.toggle_quick_menu.zones",

  "mouse.right": "ui.cancel"
}
```

---

## 🎮 手柄支持示例

### GamepadInputHandler (可选扩展)
```csharp
public class GamepadInputHandler
{
    private readonly UnifiedInputHandler _unifiedHandler;

    public void ProcessGamepad(GamePadState gamePad, out bool handled)
    {
        handled = false;

        if (gamePad.Buttons.A == ButtonState.Pressed)
        {
            var inputEvent = new GamepadInputEvent(GamepadButton.A);
            handled = _unifiedHandler.TryExecuteAction(inputEvent);
        }

        if (gamePad.Buttons.B == ButtonState.Pressed)
        {
            var inputEvent = new GamepadInputEvent(GamepadButton.B);
            handled = _unifiedHandler.TryExecuteAction(inputEvent);
        }

        // DPad navigation
        if (gamePad.DPad.Left == ButtonState.Pressed)
        {
            var inputEvent = new GamepadInputEvent(GamepadButton.DPadLeft);
            handled = _unifiedHandler.TryExecuteAction(inputEvent);
        }
    }
}
```

---

## 🔧 用户自定义键位UI示例

```csharp
// 设置界面伪代码
public class KeybindingSettingsUI
{
    private readonly InputMapper _inputMapper;

    public void OnUserWantsToRebind(string actionName)
    {
        // 1. 显示提示："Press any key for '{actionName}'"
        ShowPrompt($"Press any key for '{actionName}'");

        // 2. 等待用户按键
        var pressedKey = WaitForKeyPress();

        // 3. 更新绑定
        var eventId = $"keyboard.{pressedKey}";
        _inputMapper.SetBinding(eventId, actionName);

        // 4. 保存配置
        _inputMapper.SaveToFile("data/keybindings.json");

        // 5. 显示确认
        ShowMessage($"{actionName} now bound to {pressedKey}");
    }
}
```

---

## ✅ 优势总结

### 对比当前架构

| 特性 | 当前架构 | InputMapping架构 |
|------|---------|------------------|
| **修改键位** | ❌ 需改代码 | ✅ 改配置文件 |
| **用户自定义** | ❌ 不支持 | ✅ 完全支持 |
| **手柄支持** | ❌ 需大改 | ✅ 只需加InputEvent |
| **多输入设备** | ❌ 不支持 | ✅ 统一抽象 |
| **保存偏好** | ❌ 无法保存 | ✅ JSON配置 |
| **解耦程度** | 🟡 中等 | ✅ 完全解耦 |

### 新架构的核心优势

1. **完全数据驱动** - 所有绑定在配置文件，不在代码里
2. **设备无关** - 键盘、鼠标、手柄统一处理
3. **易于扩展** - 新增设备只需实现InputEvent
4. **用户友好** - 可以做设置界面让玩家自定义
5. **可序列化** - 绑定可以保存/加载/分享

---

## 🚀 迁移步骤

### Phase 1: 核心基础设施
1. 创建InputEvent抽象
2. 创建InputMapper
3. 创建UICommandFactory
4. 创建默认keybindings.json

### Phase 2: 替换InputHandlerComponent
1. 创建UnifiedInputHandler
2. 在FortressState中替换旧的InputHandlerComponent
3. 测试所有键位

### Phase 3: 扩展功能
1. 添加GamepadInputHandler
2. 创建键位设置UI
3. 添加配置文件加载/保存

---

## 📝 配置文件位置

```
data/
  ├── keybindings.json          # 默认键位
  ├── keybindings.user.json     # 用户自定义（优先级更高）
  └── keybindings.gamepad.json  # 手柄专用配置
```

---

## 🎯 最终架构图

```
┌─────────────┐
│   用户输入   │ (键盘/鼠标/手柄)
└──────┬──────┘
       │
       ▼
┌──────────────────┐
│   InputEvent     │ (统一抽象)
│  - KeyboardEvent │
│  - MouseEvent    │
│  - GamepadEvent  │
└────────┬─────────┘
         │
         ▼
┌─────────────────────┐
│   InputMapper       │ (映射表)
│  keybindings.json   │
└─────────┬───────────┘
          │
          ▼ ActionName
┌─────────────────────┐
│  CommandFactory     │ (命令工厂)
└─────────┬───────────┘
          │
          ▼ IUICommand
┌─────────────────────┐
│  UIStateManager     │ (执行)
└─────────────────────┘
```

---

## 结论

**当前架构**: 对于基本使用**足够**，但对你的两个需求**不够灵活**

**InputMapping架构**:
- ✅ 完全支持修改键位绑定（配置文件）
- ✅ 完全支持手柄映射（统一InputEvent）
- ✅ 完全解耦（输入设备与命令分离）
- ✅ 可扩展（新设备只需实现InputEvent）

**建议**: 如果计划长期维护，值得升级到InputMapping架构！
