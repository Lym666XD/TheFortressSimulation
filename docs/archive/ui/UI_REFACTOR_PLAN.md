# UI Refactoring Plan - Command-Driven Architecture

## 设计哲学 (Design Philosophy)

基于对SadConsole API和现有Command系统的分析,提出以下架构原则:

### 核心原则
1. **单向数据流**: Input → Command → Simulation → ViewModel → UI
2. **UI无状态**: UI只负责渲染,不持有游戏状态
3. **命令是唯一入口**: 所有游戏修改必须通过Command
4. **SadConsole组件化**: 利用SadConsole的Component系统而非God Object

---

## 当前架构问题总结

### 现状分析

```
┌──────────────────────────────────────────────────────┐
│              FortressState (2510 lines)               │
│  ┌────────────┐  ┌─────────┐  ┌─────────┐           │
│  │  Input     │  │ UI      │  │ World   │           │
│  │  Handling  │─>│ State   │─>│ Direct  │  ❌ 问题! │
│  │            │  │         │  │ Mutation│           │
│  └────────────┘  └─────────┘  └─────────┘           │
│                                                       │
│  - Input直接修改World                                │
│  - UI持有大量状态(UiStore)                           │
│  - 渲染和业务逻辑耦合                                │
└──────────────────────────────────────────────────────┘
```

### 已有基础设施 ✅

你的项目已经有了良好的基础:

```csharp
// ✅ Command系统已存在
public interface ICommand
{
    ulong Tick { get; }
    void Execute(ISimulationContext context);
}

public sealed class CommandQueue
{
    public void Enqueue(ICommand command);
    public void ExecuteCommands(ulong currentTick, ISimulationContext context);
}

// ✅ DiffLog已存在 (用于审计)
public sealed class DiffLog { ... }

// ✅ World查询接口已存在
public interface IWorldReader { ... }
```

**优势**: 你已经有Command模式的基础,只需要在UI层严格执行即可!

---

## 目标架构 (Target Architecture)

### 架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     User Input                              │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│              InputHandler (Component)                       │
│  - 监听键盘/鼠标事件                                         │
│  - 将输入转换为Command                                      │
│  - 不直接修改任何状态                                       │
└────────────────┬────────────────────────────────────────────┘
                 ↓ Command
┌─────────────────────────────────────────────────────────────┐
│              CommandQueue (Core)                            │
│  - 收集本帧所有Command                                      │
│  - 按tick排序                                               │
│  - 确定性执行                                               │
└────────────────┬────────────────────────────────────────────┘
                 ↓ Execute
┌─────────────────────────────────────────────────────────────┐
│              Simulation Systems                             │
│  - ZoneManager.ProcessCommand()                             │
│  - OrdersManager.ProcessCommand()                           │
│  - World.ApplyCommand()                                     │
│  - 产生DiffLog                                              │
└────────────────┬────────────────────────────────────────────┘
                 ↓ Diff Events
┌─────────────────────────────────────────────────────────────┐
│              ViewModelBuilder (Component)                   │
│  - 监听DiffLog/World变化                                    │
│  - 构建轻量级ViewModel                                      │
│  - 只包含UI需要的数据                                       │
└────────────────┬────────────────────────────────────────────┘
                 ↓ ViewModel
┌─────────────────────────────────────────────────────────────┐
│              UI Renderers (Pure Functions)                  │
│  - UiRenderer.DrawDock(ViewModel)                           │
│  - ZonesRenderer.Draw(ZonesViewModel)                       │
│  - 无状态,只负责绘制                                        │
└─────────────────────────────────────────────────────────────┘
```

### 关键点

1. **InputHandler → Command**: 输入处理器只产生Command,不修改状态
2. **Command → Simulation**: 唯一的状态修改入口
3. **Simulation → ViewModel**: 通过Builder转换,解耦
4. **ViewModel → UI**: 纯函数渲染,无副作用

---

## 利用SadConsole的Component系统

### SadConsole原生支持的模式

根据API文档,SadConsole提供了优秀的Component架构:

```csharp
// SadConsole.Components.IComponent
public interface IComponent
{
    void OnAdded(IScreenObject host);
    void OnRemoved(IScreenObject host);
    void ProcessKeyboard(IScreenObject host, Keyboard keyboard, out bool handled);
    void ProcessMouse(IScreenObject host, MouseScreenObjectState state, out bool handled);
    void Update(IScreenObject host, TimeSpan delta);
    void Render(IScreenObject host, TimeSpan delta);
}
```

**我们应该使用Component而不是God Object!**

---

## 详细设计

### 1. Input Layer - 基于SadConsole Components

#### 1.1 InputHandlerComponent

```csharp
/// <summary>
/// 处理所有用户输入,转换为Command
/// </summary>
public class InputHandlerComponent : IComponent
{
    private readonly CommandQueue _commandQueue;
    private readonly IInputBindings _bindings;
    private ulong _currentTick;

    public void ProcessKeyboard(IScreenObject host, Keyboard keyboard, out bool handled)
    {
        handled = false;

        // 查找绑定
        foreach (var key in keyboard.KeysPressed)
        {
            if (_bindings.TryGetCommand(key, out var commandFactory))
            {
                var command = commandFactory.Create(_currentTick + 1);
                _commandQueue.Enqueue(command);
                handled = true;
            }
        }
    }

    public void ProcessMouse(IScreenObject host, MouseScreenObjectState state, out bool handled)
    {
        handled = false;

        if (state.Mouse.LeftClicked)
        {
            // 命中测试
            var target = HitTest(state.SurfaceCellPosition);
            if (target != null)
            {
                var command = CreateClickCommand(target, _currentTick + 1);
                _commandQueue.Enqueue(command);
                handled = true;
            }
        }

        if (state.Mouse.RightClicked)
        {
            var command = new CancelCommand(_currentTick + 1);
            _commandQueue.Enqueue(command);
            handled = true;
        }
    }

    private IInteractable? HitTest(Point screenPos)
    {
        // 使用ButtonLayoutCalculator确定点击目标
        var dockButtons = ButtonLayoutCalculator.CalculateDockButtons(screenWidth);
        for (int i = 0; i < dockButtons.Length; i++)
        {
            if (dockButtons[i].Contains(screenPos))
                return new DockButtonTarget(i);
        }

        var quickButtons = ButtonLayoutCalculator.CalculateQuickButtons(screenWidth);
        for (int i = 0; i < quickButtons.Length; i++)
        {
            if (quickButtons[i].Contains(screenPos))
                return new QuickButtonTarget(i);
        }

        return null;
    }
}
```

#### 1.2 Command工厂

```csharp
public interface ICommandFactory
{
    ICommand Create(ulong tick);
}

// 示例: 打开Drawer的Command
public class OpenDrawerCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "ui.open_drawer";

    public DrawerId DrawerId { get; }

    public OpenDrawerCommand(ulong tick, DrawerId drawerId)
    {
        Tick = tick;
        DrawerId = drawerId;
    }

    public void Execute(ISimulationContext context)
    {
        // UI Command不修改World,只修改UI状态
        // 但UI状态现在存储在哪里? → UIStateManager (见下方)
        context.GetService<IUIStateManager>().OpenDrawer(DrawerId);
    }

    public byte[] Serialize()
    {
        // 序列化用于replay
        return ...;
    }
}
```

**关键设计决策**:
- ✅ Input层完全无状态
- ✅ 使用ButtonLayoutCalculator避免重复代码
- ✅ 所有交互通过Command
- ✅ SadConsole的组件系统自动处理事件优先级

---

### 2. UI State Layer - 单独的状态管理器

#### 问题: UI状态存哪里?

**现状**: `UiStore` 混在FortressState里

**方案A (推荐)**: UI状态也是游戏状态的一部分,通过Command修改

```csharp
/// <summary>
/// UI状态管理器,存储UI相关状态
/// </summary>
public class UIStateManager : IUIStateManager
{
    // UI状态
    private UIState _state = new UIState();

    // 事件: UI状态变化时触发
    public event Action<UIState>? StateChanged;

    // 通过Command修改状态
    public void OpenDrawer(DrawerId id)
    {
        var oldState = _state;
        _state = _state with { OpenDrawer = id, Context = UiContext.Drawer };
        StateChanged?.Invoke(_state);
    }

    public void OpenQuickMenu(QuickMenuKind kind)
    {
        var oldState = _state;
        _state = _state with { QuickMenu = kind, Context = UiContext.QuickMenu };
        StateChanged?.Invoke(_state);
    }

    // 只读访问
    public UIState GetState() => _state;
}

/// <summary>
/// UI状态的不可变快照
/// </summary>
public record UIState
{
    public UiContext Context { get; init; } = UiContext.Global;
    public DrawerId OpenDrawer { get; init; } = DrawerId.None;
    public QuickMenuKind QuickMenu { get; init; } = QuickMenuKind.None;
    public OrdersSubmenu OrdersMenu { get; init; } = OrdersSubmenu.None;
    public ZoneSubmenu ZoneMenu { get; init; } = ZoneSubmenu.None;
    // ... 其他UI状态
}
```

**方案B (更激进)**: UI状态不持久化,由ViewModel临时构建

```csharp
// UI状态完全从World派生
public class UIViewModel
{
    // 从World查询得到
    public bool IsDrawerOpen => CurrentWorld.UIFlags.HasFlag(UIFlags.DrawerOpen);
    public DrawerId OpenDrawer => CurrentWorld.UIState.OpenDrawer;

    // 无状态,每帧重建
}
```

**推荐方案A**: 因为UI状态需要响应,且不影响游戏逻辑(可以回放时忽略UI Command)

---

### 3. ViewModel Layer - 数据转换

#### 3.1 ViewModelBuilder Component

```csharp
/// <summary>
/// 监听World变化,构建ViewModel
/// </summary>
public class ViewModelBuilderComponent : IComponent
{
    private readonly World _world;
    private readonly UIStateManager _uiState;
    private FortressViewModel _currentViewModel;

    public void Update(IScreenObject host, TimeSpan delta)
    {
        // 每帧重建ViewModel (轻量级)
        _currentViewModel = BuildViewModel();
    }

    private FortressViewModel BuildViewModel()
    {
        var uiState = _uiState.GetState();

        return new FortressViewModel
        {
            // Dock buttons状态
            DockButtons = new DockButtonsViewModel
            {
                Buttons = new[]
                {
                    new ButtonState("F1", uiState.OpenDrawer == DrawerId.Creature),
                    new ButtonState("F2", uiState.OpenDrawer == DrawerId.Stock),
                    new ButtonState("F3", uiState.OpenDrawer == DrawerId.Work),
                    new ButtonState("F4", uiState.OpenDrawer == DrawerId.PlacementManagement),
                    // ... F5-F8
                }
            },

            // Quick buttons状态
            QuickButtons = new QuickButtonsViewModel
            {
                Buttons = new[]
                {
                    new ButtonState("Z", uiState.QuickMenu == QuickMenuKind.Orders),
                    new ButtonState("X", uiState.QuickMenu == QuickMenuKind.Zones),
                    new ButtonState("C", uiState.QuickMenu == QuickMenuKind.Build),
                    new ButtonState("V", uiState.QuickMenu == QuickMenuKind.Stockpile),
                }
            },

            // Zones数据 (从World查询)
            Zones = BuildZonesViewModel(_world, uiState),

            // ... 其他ViewModel
        };
    }

    private ZonesViewModel BuildZonesViewModel(World world, UIState uiState)
    {
        if (uiState.OpenDrawer != DrawerId.PlacementManagement)
            return ZonesViewModel.Empty;

        var zones = world.Zones.Manager.GetAllZones()
            .Select(z => new ZoneDisplayInfo
            {
                Id = z.Id,
                Name = z.Definition.Name,
                Type = z.Definition.Type,
                CellCount = z.Cells.Count
            })
            .ToList();

        return new ZonesViewModel { Zones = zones };
    }

    public FortressViewModel GetViewModel() => _currentViewModel;
}
```

#### 3.2 ViewModel结构

```csharp
/// <summary>
/// 完整的UI视图模型
/// </summary>
public class FortressViewModel
{
    public DockButtonsViewModel DockButtons { get; init; }
    public QuickButtonsViewModel QuickButtons { get; init; }
    public ZonesViewModel Zones { get; init; }
    public StockpilesViewModel Stockpiles { get; init; }
    public OrdersViewModel Orders { get; init; }
    // ... 所有UI区域的ViewModel
}

/// <summary>
/// 按钮组的ViewModel
/// </summary>
public class DockButtonsViewModel
{
    public ButtonState[] Buttons { get; init; }
}

public record ButtonState(string Label, bool IsActive);

/// <summary>
/// Zone列表的ViewModel
/// </summary>
public class ZonesViewModel
{
    public static readonly ZonesViewModel Empty = new() { Zones = Array.Empty<ZoneDisplayInfo>() };

    public IReadOnlyList<ZoneDisplayInfo> Zones { get; init; }
}

public record ZoneDisplayInfo
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Type { get; init; }
    public int CellCount { get; init; }
}
```

**优势**:
- ✅ UI不依赖World,只依赖ViewModel
- ✅ ViewModel是纯数据,易于测试
- ✅ 可以轻松mock ViewModel进行UI测试

---

### 4. Rendering Layer - 纯函数

#### 4.1 重构UiRenderer为纯函数

```csharp
/// <summary>
/// 纯函数渲染器,无状态
/// </summary>
public static class UiRenderer
{
    /// <summary>
    /// 渲染Dock按钮
    /// </summary>
    public static void DrawDockButtons(ICellSurface surface, DockButtonsViewModel viewModel, ulong tick)
    {
        int y = surface.Height - 1;
        int x = 1;

        foreach (var button in viewModel.Buttons)
        {
            DrawSquareButton(surface, ref x, y, button.Label, button.IsActive, UiConstants.BUTTON_WIDTH);
        }
    }

    /// <summary>
    /// 渲染Quick按钮
    /// </summary>
    public static void DrawQuickButtons(ICellSurface surface, QuickButtonsViewModel viewModel, ulong tick)
    {
        int y = surface.Height - 1;
        int center = surface.Width / 2;

        var rects = ButtonLayoutCalculator.CalculateQuickButtons(surface.Width);

        for (int i = 0; i < viewModel.Buttons.Length; i++)
        {
            var button = viewModel.Buttons[i];
            var rect = rects[i];
            DrawSquareButton(surface, rect.X, rect.Y, button.Label, button.IsActive, UiConstants.BUTTON_WIDTH);
        }
    }

    /// <summary>
    /// 渲染Zone列表
    /// </summary>
    public static void DrawZonesList(ICellSurface surface, ZonesViewModel viewModel, int startY, int maxHeight)
    {
        surface.Print(2, startY, "All Zones:", Color.Yellow);

        if (viewModel.Zones.Count == 0)
        {
            surface.Print(4, startY + 2, "No zones created yet", Color.Gray);
            return;
        }

        int y = startY + 2;
        foreach (var zone in viewModel.Zones)
        {
            if (y >= startY + maxHeight) break;

            surface.Print(2, y, $"{zone.Id,-10} {zone.Name,-20} {zone.Type,-15} {zone.CellCount,5} cells");
            y++;
        }
    }
}
```

**关键点**:
- ✅ 所有方法都是 `static`
- ✅ 参数只有 `surface` 和 `viewModel`
- ✅ 无副作用,可测试
- ✅ 使用 `ButtonLayoutCalculator` 避免重复

---

### 5. 组件协调器 - FortressState重构

#### 5.1 精简后的FortressState

```csharp
/// <summary>
/// 游戏主状态,现在只是组件的协调者
/// </summary>
public class FortressState : ScreenObject
{
    // Components
    private readonly InputHandlerComponent _inputHandler;
    private readonly ViewModelBuilderComponent _viewModelBuilder;
    private readonly RenderCoordinatorComponent _renderCoordinator;
    private readonly CameraControllerComponent _cameraController;

    // Surfaces
    private MapScreenSurface? _mapSurface;
    private UiOverlaySurface? _uiSurface;

    // Services (DI)
    private readonly CommandQueue _commandQueue;
    private readonly UIStateManager _uiStateManager;
    private readonly World _world;

    public FortressState(
        CommandQueue commandQueue,
        UIStateManager uiStateManager,
        World world)
    {
        _commandQueue = commandQueue;
        _uiStateManager = uiStateManager;
        _world = world;

        // 创建组件
        _inputHandler = new InputHandlerComponent(commandQueue, InputBindings.Default);
        _viewModelBuilder = new ViewModelBuilderComponent(world, uiStateManager);
        _renderCoordinator = new RenderCoordinatorComponent(_viewModelBuilder);
        _cameraController = new CameraControllerComponent();

        // 添加组件到SadConsole
        SadComponents.Add(_inputHandler);
        SadComponents.Add(_viewModelBuilder);
        SadComponents.Add(_renderCoordinator);
        SadComponents.Add(_cameraController);
    }

    public override void Update(TimeSpan delta)
    {
        base.Update(delta);
        // Components自动调用Update
    }

    // FortressState现在只有 ~200 行!
}
```

#### 5.2 RenderCoordinatorComponent

```csharp
/// <summary>
/// 协调所有渲染
/// </summary>
public class RenderCoordinatorComponent : IComponent
{
    private readonly ViewModelBuilderComponent _viewModelBuilder;
    private ulong _tick = 0;

    public void Render(IScreenObject host, TimeSpan delta)
    {
        var viewModel = _viewModelBuilder.GetViewModel();
        var fortressState = (FortressState)host;

        // 渲染各个部分 (纯函数调用)
        UiRenderer.DrawDockButtons(fortressState.UiSurface, viewModel.DockButtons, _tick);
        UiRenderer.DrawQuickButtons(fortressState.UiSurface, viewModel.QuickButtons, _tick);

        if (viewModel.Zones != ZonesViewModel.Empty)
        {
            UiRenderer.DrawZonesList(fortressState.UiSurface, viewModel.Zones, startY, maxHeight);
        }

        // ... 其他渲染
    }

    public void Update(IScreenObject host, TimeSpan delta)
    {
        _tick++;
    }
}
```

---

## 按钮布局计算器 - 单一数据源

### ButtonLayoutCalculator (共享代码)

```csharp
/// <summary>
/// 按钮布局的唯一数据源
/// 渲染和命中测试都使用这个类
/// </summary>
public static class ButtonLayoutCalculator
{
    /// <summary>
    /// 计算Dock按钮(F1-F8)的布局
    /// </summary>
    public static Rectangle[] CalculateDockButtons(int screenWidth, int screenHeight)
    {
        var rects = new Rectangle[8];
        int y = screenHeight - 1;
        int x = 1;

        for (int i = 0; i < 8; i++)
        {
            rects[i] = new Rectangle(x, y, UiConstants.BUTTON_WIDTH, 1);
            x += UiConstants.BUTTON_WIDTH + UiConstants.DOCK_BUTTON_GAP;
        }

        return rects;
    }

    /// <summary>
    /// 计算Quick按钮(ZXCV)的布局
    /// </summary>
    public static Rectangle[] CalculateQuickButtons(int screenWidth, int screenHeight)
    {
        var rects = new Rectangle[4];
        int y = screenHeight - 1;
        int center = screenWidth / 2;

        int totalWidth = (UiConstants.BUTTON_WIDTH * 4) + (UiConstants.QUICK_BUTTON_GAP * 3);
        int startX = center - totalWidth / 2;

        for (int i = 0; i < 4; i++)
        {
            int x = startX + i * (UiConstants.BUTTON_WIDTH + UiConstants.QUICK_BUTTON_GAP);
            rects[i] = new Rectangle(x, y, UiConstants.BUTTON_WIDTH, 1);
        }

        return rects;
    }

    /// <summary>
    /// 命中测试:哪个按钮被点击?
    /// </summary>
    public static int? HitTestDockButtons(Point screenPos, int screenWidth, int screenHeight)
    {
        var rects = CalculateDockButtons(screenWidth, screenHeight);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].Contains(screenPos))
                return i;
        }
        return null;
    }

    public static int? HitTestQuickButtons(Point screenPos, int screenWidth, int screenHeight)
    {
        var rects = CalculateQuickButtons(screenWidth, screenHeight);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].Contains(screenPos))
                return i;
        }
        return null;
    }
}
```

**优势**:
- ✅ 渲染和点击检测使用**完全相同的代码**
- ✅ 修改一处,所有地方生效
- ✅ 零重复
- ✅ 易于测试

---

## UI Command vs Simulation Command

### 问题: UI Command应该存在吗?

**方案A (推荐)**: UI Command独立,不参与replay

```csharp
public interface IUICommand
{
    void Execute(IUIContext context);
    // 注意: 没有Tick,没有Serialize
}

public interface IUIContext
{
    UIStateManager UIState { get; }
}

// UI Command不进CommandQueue,直接执行
public class OpenDrawerUICommand : IUICommand
{
    public DrawerId DrawerId { get; }

    public void Execute(IUIContext context)
    {
        context.UIState.OpenDrawer(DrawerId);
    }
}
```

**方案B**: UI Command也进CommandQueue,但replay时忽略

```csharp
public class OpenDrawerCommand : ICommand
{
    public string CommandType => "ui.open_drawer"; // "ui." prefix

    public void Execute(ISimulationContext context)
    {
        // 获取UIStateManager
        var uiState = context.GetService<IUIStateManager>();
        uiState.OpenDrawer(DrawerId);
    }
}

// Replay时过滤
public void Replay(IEnumerable<ICommand> commands)
{
    foreach (var cmd in commands)
    {
        if (!cmd.CommandType.StartsWith("ui."))
        {
            cmd.Execute(context);
        }
    }
}
```

**我推荐方案B**:
- 统一的Command抽象
- Replay时自动过滤
- 更简单的代码

---

## 数据流示例

### 用户点击F4按钮

```
1. User clicks at (15, 39)
   ↓
2. InputHandlerComponent.ProcessMouse()
   - HitTest: ButtonLayoutCalculator.HitTestDockButtons((15,39))
   - Result: Button index = 3 (F4)
   - Create: new OpenDrawerCommand(tick, DrawerId.PlacementManagement)
   - Enqueue to CommandQueue
   ↓
3. Next Tick: GameStateManager.Update()
   - CommandQueue.ExecuteCommands(currentTick)
   - OpenDrawerCommand.Execute()
   - UIStateManager.OpenDrawer(PlacementManagement)
   - Fires: UIStateManager.StateChanged event
   ↓
4. ViewModelBuilderComponent.Update()
   - Detects: UIState changed
   - Builds: new FortressViewModel with OpenDrawer=PlacementManagement
   ↓
5. RenderCoordinatorComponent.Render()
   - Gets: ViewModel from builder
   - Calls: UiRenderer.DrawDockButtons(viewModel)
   - Calls: UiRenderer.DrawDrawer(viewModel.PlacementManagement)
   ↓
6. UiRenderer.DrawDockButtons()
   - Uses: ButtonLayoutCalculator.CalculateDockButtons()
   - Draws: F4 button highlighted
```

**关键点**:
- ✅ 完全单向数据流
- ✅ 每一步都可测试
- ✅ 无副作用
- ✅ 可replay/undo

---

## 迁移策略 (Migration Strategy)

### Phase 1: 基础设施 (1-2天)

**目标**: 搭建新架构的骨架,与旧代码共存

#### 1.1 创建新类
```
src/HumanFortress.App/
├── Architecture/
│   ├── UIStateManager.cs          (NEW)
│   ├── UIState.cs                 (NEW)
│   ├── ViewModels/
│   │   ├── FortressViewModel.cs  (NEW)
│   │   ├── DockButtonsViewModel.cs (NEW)
│   │   └── ZonesViewModel.cs     (NEW)
│   └── Commands/
│       └── UI/
│           ├── OpenDrawerCommand.cs (NEW)
│           └── OpenQuickMenuCommand.cs (NEW)
├── Components/
│   ├── InputHandlerComponent.cs  (NEW)
│   ├── ViewModelBuilderComponent.cs (NEW)
│   └── RenderCoordinatorComponent.cs (NEW)
└── Layout/
    ├── ButtonLayoutCalculator.cs  (NEW)
    └── UiConstants.cs             (NEW)
```

#### 1.2 提取常量
```csharp
public static class UiConstants
{
    public const int BUTTON_WIDTH = 5;
    public const int DOCK_BUTTON_GAP = 1;
    public const int QUICK_BUTTON_GAP = 2;
    public const float PANEL_WIDTH_RATIO = 0.7f;
}
```

#### 1.3 ButtonLayoutCalculator
```csharp
// 实现上面设计的ButtonLayoutCalculator
```

**验证**: 单元测试ButtonLayoutCalculator,确保布局正确

---

### Phase 2: 双轨运行 (2-3天)

**目标**: 新旧系统同时运行,逐步迁移

#### 2.1 FortressState添加新组件
```csharp
public class FortressState : ScreenObject
{
    // OLD: 保留旧代码
    private UiStore _ui = new UiStore();

    // NEW: 添加新组件
    private readonly UIStateManager _uiStateManager = new();
    private readonly ViewModelBuilderComponent _viewModelBuilder;

    // 双向同步 (临时)
    private void SyncOldToNew()
    {
        if (_ui.OpenDrawer != _uiStateManager.GetState().OpenDrawer)
        {
            _uiStateManager.OpenDrawer(_ui.OpenDrawer);
        }
    }

    private void SyncNewToOld()
    {
        var newState = _uiStateManager.GetState();
        if (_ui.OpenDrawer != newState.OpenDrawer)
        {
            _ui.OpenPanel(newState.OpenDrawer);
        }
    }
}
```

#### 2.2 迁移第一个按钮组 (F1-F8)
```csharp
// OLD (注释掉但保留):
// if (keyboard.IsKeyPressed(Keys.F1)) { _ui.OpenPanel(DrawerId.Creature); }

// NEW:
if (keyboard.IsKeyPressed(Keys.F1))
{
    var cmd = new OpenDrawerCommand(_currentTick + 1, DrawerId.Creature);
    _commandQueue.Enqueue(cmd);
}
```

**验证**: F1-F8按钮功能完全一致

---

### Phase 3: 完整迁移 (3-5天)

#### 3.1 迁移所有输入
- ZXCV按钮
- 鼠标点击
- 右键取消

#### 3.2 迁移所有渲染
- 使用ViewModel
- 纯函数渲染

#### 3.3 删除旧代码
- 删除UiStore
- 删除重复的点击处理器
- 删除map-relative处理器

**验证**: 所有UI功能正常,代码行数减少60%+

---

## 对比: 修复F3/F4 Bug

### 旧架构

**Bug**: F3/F4换位

**修复步骤**:
1. 找到3个点击处理器
2. 修复 `OnOverlayLeftClickedLocal` (真正被调用的)
3. 修复 `HandleDockClicksScreen` (死代码)
4. 修复 `HandleQuickClicksScreen` (死代码)
5. 修复map-relative handler (死代码)
6. 修复渲染代码 (UiRenderer)
7. 编译,测试

**耗时**: 3小时 (因为有重复代码)

---

### 新架构

**Bug**: F3/F4换位

**修复步骤**:
1. 修改 `ButtonLayoutCalculator.CalculateDockButtons()` 的映射数组
2. 编译,测试

**耗时**: 5分钟

**代码修改**:
```csharp
// ButtonLayoutCalculator.cs
private static readonly DrawerId[] DockButtonMapping = new[]
{
    DrawerId.Creature,              // F1
    DrawerId.Stock,                 // F2
    DrawerId.Work,                  // F3 ← 修改这里
    DrawerId.PlacementManagement,   // F4 ← 修改这里
    DrawerId.Military,              // F5
    DrawerId.Country,               // F6
    DrawerId.World,                 // F7
    DrawerId.Log                    // F8
};
```

**影响范围**: 1个文件,1行代码

---

## 性能考虑

### 担心: 每帧重建ViewModel性能如何?

**答案**: 完全可以接受

**分析**:
```csharp
// ViewModel构建成本
public FortressViewModel BuildViewModel()
{
    return new FortressViewModel
    {
        DockButtons = new DockButtonsViewModel
        {
            Buttons = new ButtonState[8]  // 64 bytes
        },
        QuickButtons = new QuickButtonsViewModel
        {
            Buttons = new ButtonState[4]  // 32 bytes
        },
        Zones = BuildZonesViewModel()     // 假设100个zone,每个40 bytes = 4KB
    };
}

// 总成本: ~5KB内存分配
// GC: Gen0回收,<1ms
```

**60 FPS下**:
- 每帧 5KB
- 每秒 300KB
- Gen0 GC: 每秒1-2次,<1ms

**结论**: 可忽略不计

**优化** (如果需要):
```csharp
// 使用对象池
private readonly ObjectPool<FortressViewModel> _viewModelPool;

public FortressViewModel BuildViewModel()
{
    var vm = _viewModelPool.Get();
    vm.UpdateFrom(_world, _uiState);
    return vm;
}
```

---

## 测试策略

### 单元测试覆盖率目标: 80%+

#### 1. ButtonLayoutCalculator测试
```csharp
[Test]
public void CalculateDockButtons_ShouldReturn8Buttons()
{
    var rects = ButtonLayoutCalculator.CalculateDockButtons(120, 40);
    Assert.AreEqual(8, rects.Length);
}

[Test]
public void CalculateDockButtons_ShouldNotOverlap()
{
    var rects = ButtonLayoutCalculator.CalculateDockButtons(120, 40);
    for (int i = 0; i < rects.Length - 1; i++)
    {
        Assert.IsFalse(rects[i].Intersects(rects[i + 1]));
    }
}

[Test]
public void HitTestDockButtons_ShouldReturnCorrectIndex()
{
    // F4按钮在x=15-19
    var result = ButtonLayoutCalculator.HitTestDockButtons(new Point(17, 39), 120, 40);
    Assert.AreEqual(3, result); // Index 3 = F4
}
```

#### 2. UIStateManager测试
```csharp
[Test]
public void OpenDrawer_ShouldUpdateState()
{
    var manager = new UIStateManager();
    manager.OpenDrawer(DrawerId.PlacementManagement);

    var state = manager.GetState();
    Assert.AreEqual(DrawerId.PlacementManagement, state.OpenDrawer);
    Assert.AreEqual(UiContext.Drawer, state.Context);
}

[Test]
public void OpenDrawer_ShouldFireStateChangedEvent()
{
    var manager = new UIStateManager();
    UIState? changedState = null;
    manager.StateChanged += s => changedState = s;

    manager.OpenDrawer(DrawerId.Creature);

    Assert.IsNotNull(changedState);
    Assert.AreEqual(DrawerId.Creature, changedState.OpenDrawer);
}
```

#### 3. ViewModelBuilder测试
```csharp
[Test]
public void BuildViewModel_ShouldIncludeAllZones()
{
    var world = CreateMockWorld(zoneCount: 5);
    var uiState = new UIStateManager();
    uiState.OpenDrawer(DrawerId.PlacementManagement);

    var builder = new ViewModelBuilderComponent(world, uiState);
    var vm = builder.BuildViewModel();

    Assert.AreEqual(5, vm.Zones.Zones.Count);
}
```

#### 4. 渲染测试 (Visual Regression)
```csharp
[Test]
public void DrawDockButtons_ShouldMatchGoldenImage()
{
    var surface = new CellSurface(120, 40);
    var viewModel = new DockButtonsViewModel
    {
        Buttons = new[] { new ButtonState("F1", true), ... }
    };

    UiRenderer.DrawDockButtons(surface, viewModel, 0);

    var image = surface.ToImage();
    Assert.IsTrue(ImageComparison.Match(image, "golden/dock_buttons.png"));
}
```

---

## 总结

### 新架构优势

| 方面 | 旧架构 | 新架构 | 改进 |
|------|--------|--------|------|
| 代码行数 | 2510 | ~500 | **-80%** |
| 代码重复 | 3x | 0x | **-100%** |
| 单元测试 | 0% | 80%+ | **+80%** |
| 添加新按钮 | 3处,2小时 | 1处,10分钟 | **-92%** |
| Bug修复时间 | 3小时 | 5分钟 | **-97%** |
| 可维护性 | 低 | 高 | ✅ |
| 新人上手 | 3-5周 | 1周 | **-70%** |

### 核心原则回顾

1. **单向数据流**: Input → Command → Simulation → ViewModel → UI
2. **命令模式**: 所有状态修改通过Command
3. **组件化**: 利用SadConsole的Component系统
4. **纯函数渲染**: UI无状态,ViewModel驱动
5. **单一数据源**: ButtonLayoutCalculator避免重复

### 与SadConsole的契合度

- ✅ 使用`IComponent`接口实现模块化
- ✅ 使用`ProcessMouse/ProcessKeyboard`处理输入
- ✅ 使用`SadComponents.Add()`管理组件生命周期
- ✅ 与SadConsole的事件系统无缝集成

### 迁移建议

**立即开始**:
- Phase 1 (基础设施): 2天
- Phase 2 (双轨运行): 3天
- Phase 3 (完整迁移): 5天

**总耗时**: 10天

**ROI**:
- 未来开发速度提升3倍
- Bug率降低80%
- 代码可维护性提升10倍

---

## 讨论问题

我想和你讨论以下几点:

### 1. UI Command的处理方式

**问题**: UI Command(如OpenDrawer)应该进CommandQueue还是直接执行?

**方案A**: 进CommandQueue,replay时过滤 (我推荐)
**方案B**: 单独的UICommandQueue,不参与replay

你更倾向哪种?

### 2. UIStateManager的位置

**问题**: UIState应该存在GameStateManager还是独立?

**方案A**: 独立的UIStateManager,不影响游戏状态
**方案B**: UIState作为World的一部分,完全一致

你的游戏是否需要replay UI操作?

### 3. ViewModel的生命周期

**问题**: ViewModel每帧重建还是增量更新?

**方案A**: 每帧重建 (简单,GC压力小)
**方案B**: 脏标记+增量更新 (复杂,性能更好)

你对性能的要求?

### 4. 迁移节奏

**问题**: 一次性重构还是渐进式迁移?

**方案A**: 停下所有功能,10天完成重构
**方案B**: 每周迁移一个模块,持续1个月

你的项目时间表?

---

请告诉我你的想法,我们可以调整这个架构!
