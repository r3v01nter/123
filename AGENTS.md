# AGENTS.md — AI 协作与开发约束规范

## Project Identity
Windows Island Media Controller (WIMC)

## Core Constraints

1. 禁止 explorer.exe 注入、DLL Injection、Hook、未公开 Taskbar 内部结构依赖。
2. 媒体控制必须使用公开 API Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager (GSMTC)。
3. 窗口必须保持 Non-Activating，不得抢占用户焦点，使用 WS_EX_NOACTIVATE 等 Win32 属性。
4. 优先保证 Windows 更新后的长期稳定性。

## Technology Stack

- C# 12
- .NET 8+
- Windows App SDK
- WinUI 3
- WinRT GSMTC
- DWM / Win32 Interop

## Architecture

src/
├── WindowsIsland.Core/  # 媒体、Win32、定位核心
├── WindowsIsland.UI/   # WinUI 3 界面与动画
└── WindowsIsland.App/  # 应用入口

## Current Phase

Phase 0: 文档与规范建立。

禁止在未完成架构设计前生成大量业务代码。

## Project Philosophy

目标是创建一个独立、开源、稳定的 Windows 动态媒体岛软件，而不是复制依赖系统内部结构的 Mod。
