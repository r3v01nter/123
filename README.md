# Windows Island Media Controller (WIMC)

> 🏝️ Windows 动态媒体岛控制软件

## 项目愿景

WIMC 是一个面向 Windows 11 的开源动态媒体岛软件。

项目参考 Windhawk 的 Island Media Controls Mod，但不采用 explorer.exe 注入、Hook 或未公开任务栏内部结构依赖方案。

目标是基于 Windows 官方公开 API，打造稳定、美观、可长期维护的媒体控制体验。

## 项目背景

传统任务栏增强方案可能因为 Windows 更新导致：

- 定位失效
- 无法吸附任务栏
- 窗口层级异常
- 系统升级后失效

WIMC 希望从架构层面解决这些问题。

## 核心技术方向

- C# / .NET 8+
- Windows App SDK
- WinUI 3
- Windows.Media.Control GSMTC
- DWM 与 Win32 窗口管理

## Roadmap

- [x] Phase 0：项目规划、协作规范、许可证与设计文档 **[已完成]**
- [ ] Phase 1：媒体核心与窗口原型
- [ ] Phase 2：动态岛 UI 与动画
- [ ] Phase 3：任务栏感知与多屏适配
- [ ] Phase 4：开源发布

## License

MIT License
