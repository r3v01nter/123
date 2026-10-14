# WIMC 技术设计文档

> 说明：本文档定义三层架构、核心 API 流程与资源管理。存在不确定性的技术点均在文末标记为 **[待验证原型项]**。

## 1. 三层工程结构与依赖方向
采用单向依赖：`WindowsIsland.App` -> `WindowsIsland.UI` -> `WindowsIsland.Core`。
- **WindowsIsland.Core**：目标框架 `net8.0-windows10.0.22621.0`，封装 GSMTC 媒体服务、Win32/DWM P/Invoke 与坐标计算，不包含任何 XAML 控件引用，支持独立原型验证。
- **WindowsIsland.UI**：承载 WinUI 3 窗口、XAML 视图、ViewModel 与动效控制，仅通过接口调用 Core 层。
- **WindowsIsland.App**：负责单实例检查、全局异常捕获与启动生命周期。

## 2. GSMTC 媒体会话服务设计
- **会话获取与切换**：调用 `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`，订阅 `CurrentSessionChanged` 与 `SessionsChanged`。切换会话时，**必须先显式取消订阅（`-=`）旧会话事件**再绑定新会话，严防事件句柄泄漏。
- **元数据与状态读取**：
  - `TryGetMediaPropertiesAsync()`：读取 `Title`、`Artist`、`AlbumTitle` 与 `Thumbnail` 流引用。
  - `GetPlaybackInfo()`：读取 `PlaybackStatus`（Playing/Paused）与控制能力位（`IsNextEnabled` 等）。
  - `GetTimelineProperties()`：读取播放进度。
- **播放控制与异常防护**：调用 `TryTogglePlayPauseAsync()`、`TrySkipNextAsync()`、`TrySkipPreviousAsync()`，外层必须捕获 `COMException`（防止目标播放器突然关闭导致崩溃）。
- **无会话与防抖**：`GetCurrentSession()` 为 `null` 时通知 UI 进入休眠态；对 `MediaPropertiesChanged` 增加 100ms 防抖合并，避免切歌瞬间多次刷新闪烁。

## 3. 封面流缓存与资源释放
- **指纹比对**：维护 `LastTrackKey = $"{Title}|{Artist}|{AlbumTitle}"`。若曲目未变且已有封面缓存，跳过流读取。
- **确定性释放**：使用 `using var stream = await thumbnailRef.OpenReadAsync()` 拷贝为字节数组后立即关闭底层 WinRT 流；UI 层在 `DispatcherQueue` 中更新 `BitmapImage` 并释放旧引用。

## 4. 非激活置顶窗口与输入交互
- **扩展样式**：获取窗口 `HWND`，通过 `SetWindowLongPtr(GWL_EXSTYLE)` 附加 `WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`。
- **消息拦截**：通过 `SetWindowSubclass` 子类化 `WndProc`，拦截 `WM_MOUSEACTIVATE (0x0021)` 并返回 `MA_NOACTIVATE (3)`，指示系统不激活窗口但继续将鼠标点击派发给内部控件。
- **输入边界**：窗口正常接收鼠标悬停与点击，但默认不接收键盘输入焦点（MVP 阶段不设文本输入框）。

## 5. 模块职责边界
- `MediaSessionService` (Core)：管理 GSMTC 会话、防抖与控制，严禁引用 UI 类型。
- `WindowStyleService` (Core)：管理 Win32 样式与 `WndProc` 子类化。
- `TaskbarDetector` (Core)：探测任务栏与工作区坐标。
- `IslandViewModel` / `IslandWindow` (UI)：调度 UI 线程更新绑定属性与视图状态。

## 6. 待验证原型项清单 (To-Be-Verified)
以下假设必须在 Phase 1 前两步通过小型原型实测确认后方可大规模编码：
1. **[TV-01] 非激活窗口内 WinUI 3 Button 点击路由**：实测设置 `WS_EX_NOACTIVATE` + `MA_NOACTIVATE` 后，点击 `<Button>` 是否能一次触发 `Click` 且不夺取记事本打字焦点。
2. **[TV-02] 无边框圆角透明窗口合成**：实测隐藏系统边框标题栏后四周是否有残留白边或阴影异常。
3. **[TV-03] 多源会话优先级**：实测浏览器后台暂停视频与音乐软件同时存在时，`GetCurrentSession()` 是否准确指向正在播放的会话。
