# 更新记录

## 1.0.1 — 2026-10-27

- 移除多余的 `IOverloadOnBattleInitScript` 备用钩子，避免雷达排在 FloatingHPBar 前面时抢占血条的启动入口。
- 保留原有属性启动、相机和日志回调；挖宝数据读取、绘制与配置默认值不变。
- 新增 Windows 钩子选择检查，调用安装的 Memoria 引擎验证两种顺序和单独启用场景。
- 自动检查通过；1.0.1 尚未进行单独的游戏内回归。

### English

- Remove the redundant battle-init fallback so the radar no longer competes with FloatingHPBar for the exclusive startup hook.
- Preserve the existing attribute-based startup, camera/log callbacks, treasure detection, rendering and configuration defaults.
- Add a Windows regression check using the installed Memoria resolver for both mod orders and each mod alone.
- Automated checks passed; this patch has not received a separate in-game regression pass.

## 1.0 — 发布准备

- 保留现有功能源码及版本号。
- 整理安装、兼容性与构建说明，提供 Windows 构建和发布包脚本。
- 发布候选 DLL 由当前源码重新编译；未宣称完成游戏内回归。
