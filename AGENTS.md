# AGENTS.md — AI 协作与开发约束规范

## Project Identity
Windows Island Media Controller (WIMC)

## Core Constraints

1. 禁止 explorer.exe 注入、DLL Injection、Hook、未公开 Taskbar 内部结构依赖。
2. 媒体控制必须使用公开 API Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager (GSMTC)。
3. 窗口必须保持 Non-Activating，不得抢占用户焦点；使用 WS_EX_NOACTIVATE 等 Win32 属性，但必须以真实 Windows 运行测试验证交互行为。
4. 优先保证 Windows 更新后的长期稳定性。
5. 不得把 CI 构建成功等同于 GUI 运行验收通过；报告中必须区分已构建、已静态审查、已实际测试。

## Technology Stack

- C# 12
- .NET 8+
- WinRT GSMTC
- Win32 Interop
- 目标 UI 技术：Windows App SDK / WinUI 3（尚未在当前原型中实现）

## Current Repository Architecture

- \`src/WindowsIsland.Core/\`：媒体、Win32、定位核心。
- \`src/WindowsIsland.Prototype/\`：当前原生 Win32 窗口与 GSMTC 原型。
- 当前仓库尚未包含已实现的 \`WindowsIsland.UI\` 或 \`WindowsIsland.App\` 项目。

## Current Phase

- **Phase 0：文档与规范建立 — 已完成。**
- **Phase 1：媒体核心与窗口原型 — 已实现初始原型，构建/打包已成功；真实桌面交互验收仍待完成。**
- 当前优先事项：验证焦点保持、鼠标交互、媒体会话行为与退出清理。
- 在关键原型验收前，不得无必要地扩展大量业务功能。

## Project Philosophy

目标是创建一个独立、开源、稳定的 Windows 动态媒体岛软件，而不是复制依赖系统内部结构的 Mod。
