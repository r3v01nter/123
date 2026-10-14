# WIMC 技术设计文档

> 状态说明：本文档包含目标架构与当前原型实现。凡标记为“待验证”的内容，都不能仅凭 CI 构建成功视为已通过。

## 1. 目标架构与当前仓库状态

目标架构采用单向依赖：\`WindowsIsland.App\` -> \`WindowsIsland.UI\` -> \`WindowsIsland.Core\`。
- **WindowsIsland.Core（当前已有）**：目标框架 \`net8.0-windows10.0.22621.0\`，封装 GSMTC 媒体服务、Win32/DWM P/Invoke 与坐标计算，不包含 XAML 控件引用。
- **WindowsIsland.UI（目标组件，尚未实现）**：计划承载 WinUI 3 窗口、XAML 视图、ViewModel 与动效控制，仅通过接口调用 Core 层。
- **WindowsIsland.App（目标组件，尚未实现）**：计划负责单实例检查、全局异常捕获与启动生命周期。
- **WindowsIsland.Prototype（当前已有）**：当前验证程序使用原生 Win32 窗口，不是 WinUI 3 窗口；它可用于初步检查 Win32 窗口行为，但不能替代 WinUI 3 Button 点击路由验证。

## 2. GSMTC 媒体会话服务设计

- **会话获取与切换**：调用 \`GlobalSystemMediaTransportControlsSessionManager.RequestAsync()\`，订阅 \`CurrentSessionChanged\` 与 \`SessionsChanged\`。切换会话时，必须先取消订阅旧会话事件再绑定新会话。
- **元数据与状态读取**：
  - \`TryGetMediaPropertiesAsync()\`：读取 \`Title\`、\`Artist\`、\`AlbumTitle\` 与 \`Thumbnail\` 流引用。
  - \`GetPlaybackInfo()\`：读取播放状态与控制能力位。
  - \`GetTimelineProperties()\`：读取播放进度。
- **播放控制与异常防护**：使用 \`TryTogglePlayPauseAsync()\`、\`TrySkipNextAsync()\`、\`TrySkipPreviousAsync()\`，处理目标播放器关闭或 COM 调用失败的情况。
- **无会话与防抖**：无活动会话时通知订阅方；媒体状态变化采用 100ms 防抖合并。

## 3. 封面流缓存与资源释放

- 以应用 ID、曲名、艺人和专辑构成曲目键，曲目未变且已有封面缓存时跳过重复读取。
- 封面流复制为字节数组后释放流对象。
- 当前原型尚未实现 UI 层的 BitmapImage 更新流程；未来 UI 更新应在对应 UI 线程执行，并妥善处理旧资源引用。

## 4. 非激活置顶窗口与输入交互

目标方案：
- 通过 \`SetWindowLongPtr(GWL_EXSTYLE)\` 设置 \`WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE\`。
- 通过 \`SetWindowSubclass\` 处理 \`WM_MOUSEACTIVATE\` 并返回 \`MA_NOACTIVATE\`，意图是在不激活窗口的情况下保留鼠标消息路由。

当前原生 Win32 原型：
- 窗口创建时设置非激活相关扩展样式，并调用 \`WindowStyleService.AttachNonActivatingOverlay\`。
- 当前按钮通过自定义 Win32 消息处理，不是 WinUI 3 Button。
- 点击后会记录前后前台窗口句柄是否一致，但该日志只是有限的诊断信息，不能单独证明焦点行为在所有场景下正确。
- **必须在真实 Windows 环境验证**：点击按钮是否能执行操作且不抢夺记事本等应用的输入焦点。

## 5. 模块职责边界

- \`MediaSessionService\`（Core）：管理 GSMTC 会话、防抖与媒体控制，不引用 UI 类型。
- \`WindowStyleService\`（Core）：管理 Win32 窗口样式与子类化。
- \`TaskbarDetector\`（Core）：探测任务栏与工作区坐标。
- \`OverlayTestHarness\`（Prototype）：当前 Win32 原型界面和消息循环。

## 6. 待验证原型项清单

1. **[TV-01] 非激活窗口交互**：当前 Win32 原型中，点击上一首、播放/暂停、下一首时，能否保持其他应用的输入焦点；关闭按钮能否正常关闭窗口。
2. **[TV-02] WinUI 3 Button 点击路由**：目标 UI 尚未实现，需在后续 WinUI 3 原型中单独验证 \`WS_EX_NOACTIVATE\` + \`MA_NOACTIVATE\` 对控件点击事件的影响。
3. **[TV-03] 无边框圆角透明窗口合成**：当前原型未覆盖，待 WinUI 3 UI 原型阶段验证。
4. **[TV-04] 多源会话优先级**：浏览器后台暂停视频与音乐软件同时存在时，当前会话选择策略是否符合预期。
5. **[TV-05] 异常与资源清理**：媒体会话切换、媒体应用退出、窗口初始化失败及程序退出时的资源清理，需结合代码审查和运行测试确认。
