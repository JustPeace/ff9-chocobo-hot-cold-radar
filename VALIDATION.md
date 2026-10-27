# 1.0.1 validation — 2026-10-27

Baseline: installed Windows Steam FF9, Memoria 2025-07-13. Compiler: stable .NET SDK 9.0.300. Author: JustPeace. License: MIT.

## Actual engine regression check

`tools/HookResolutionCheck.cs` uses reflection to invoke the installed engine's real `ScriptsLoader.GetOverloadedMethod` method. It constructs the loader's own result records from the mod types' actual implemented interfaces. It does not start Unity, instantiate startup attributes, execute `OnBattleInit`, change game configuration, or write saves.

| Inputs (highest priority first) | Original radar 1.0 | Patched radar 1.0.1 |
| --- | --- | --- |
| FloatingHPBar, radar | FloatingHPBar hook | FloatingHPBar hook |
| Radar, FloatingHPBar | Radar hook | FloatingHPBar hook |
| FloatingHPBar alone | FloatingHPBar hook | FloatingHPBar hook |
| Radar alone | Radar hook | No battle-init hook |

All cases passed. In the patch, the startup attribute is still attached to the marker type, and `InstallMainThreadPump` / `Ensure` are retained. Attribute metadata is inspected without executing Unity code.

Run after `build.ps1` (requires Windows .NET Framework 4.x and FloatingHPBar installed in the game directory):

```powershell
./tools/verify-hooks.ps1 -GamePath 'D:/Games/FINAL FANTASY IX'
```

To reproduce the old behavior using a separately retained 1.0 DLL:

```powershell
./tools/verify-hooks.ps1 -GamePath 'D:/Games/FINAL FANTASY IX' -RadarDll 'D:/backups/Memoria.Scripts.ChocoboHotCold.dll' -ExpectLegacy
```

## Build and package checks

- Source compilation succeeds against the installed engine references.
- Functional source diff is limited to removing the marker's battle-init interface/method; the runtime version log changes to 1.0.1.
- Treasure detection, event-variable reads, rendering, callback installation and configuration parsing are unchanged.
- ZIP contains a single `ChocoboHotCold` root, this mod's own DLL, configuration, metadata, English/Chinese READMEs, MIT license, checklist and this report.
- Archive contents match staged files; SHA-256 matches the supplied checksum. No game/engine DLLs or saves are included.

## Limits

The author reports that the original installed version works in-game. Patch 1.0.1 has not received a separate in-game regression pass. These checks prove hook selection and preserve the existing startup structure; they do not prove Unity callback execution, drawing or behavior in every minigame scene. Large overhauls and other Memoria versions remain unverified.

## 中文说明

已直接调用本地引擎的实际钩子选择方法，对旧版复现顺序依赖，并验证新版在两种顺序下都保留血条的战斗钩子。雷达单独启用时不再占用该钩子，原有启动属性及独立入口保留。编译、代码差异和安装包检查通过。测试不会启动游戏或写入存档；1.0.1 尚未单独进行实机场景回归。
