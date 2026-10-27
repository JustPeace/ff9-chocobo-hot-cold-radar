# Chocobo Hot & Cold Radar 1.0.1

为陆行鸟挖宝显示宝藏位置、方位和距离。


[English](README.md) | 简体中文

## 环境与兼容性

- Windows Steam 版 FINAL FANTASY IX，已安装 [Memoria](https://github.com/Albeoris/Memoria/releases/tag/v2025.07.04) 的 2025-07-13 热修版本。
- 2025-07-13 是本次发布准备使用的编译与兼容基准；更早版本和 Canary 未验证。
- 1.0.1 已通过源码编译、包内容及本地引擎两种加载顺序验证。作者反馈原安装版本实机正常；1.0.1 尚未单独进行游戏内回归。详见 [发布检查清单](RELEASE-CHECKLIST.md)。
- 不包含游戏、Memoria、汉化或 Moguri 的 DLL、素材和存档。游戏引用需从自己的安装目录读取。


## 安装

1. 从 Releases 下载 `ChocoboHotCold-v1.0.1.zip` 并退出游戏。
2. 解压到游戏根目录（有 `FF9_Launcher.exe` 的目录），确认形成 `ChocoboHotCold/StreamingAssets/Scripts/Memoria.Scripts.ChocoboHotCold.dll`，不要套两层文件夹。
3. 在 Memoria 启动器的 Mods 页面启用本 mod，然后重启游戏。
4. 如果启动器未列出它，在 `Memoria.ini` 的 `[Mod]` 中，将 `"ChocoboHotCold"` 追加到现有 `FolderNames` 和 `Priorities` 列表。保留其他 mod 的名称与顺序，不要用示例覆盖整行。


## 卸载

退出游戏，在启动器中停用本 mod，或从 `FolderNames` / `Priorities` 移除其名称，再删除 `ChocoboHotCold` 文件夹。

## 配置

编辑 `ChocoboHotCold/ChocoboHotCold.ini`，修改后请退出并重启游戏。当前代码只在覆盖层首次创建时读取配置：

| 选项 | 默认 | 说明 |
| --- | --- | --- |
| Enabled | 1 | 总开关 |
| ShowWorldMarker | 1 | 场景内标记 |
| ShowCompass | 1 | 右上角罗盘 |
| ShowDistance | 1 | 显示距离 |
| ShowKweh | 1 | 显示 Kweh 强度点 |
| ShowAllPoints | 0 | 显示全部 9 个候选点（调试） |
| OnlyDuringMinigame | 0 | 只在计时进行中显示 |
| UnitsPerMeter | 100 | 多少游戏单位算 1 米 |
| Scale | 1.0 | UI 缩放 |
| MarkerHeight | 70 | 世界标记离地高度 |


## 原理（逆向自游戏自带事件脚本）

三个挖宝场地在进场时会用 `InitObject` 创建 9 个隐藏的挖掘点对象，uid 为 128~136，坐标由事件脚本用随机数生成。

小游戏每轮从这些点中随机选一个作为宝藏，并把 **1 基索引写入地图变量 `#37`（SByte）**；同时把到该点的距离写入脚本实例变量，用来决定 Choco 的 `SEEK_1..5` 动画。全局变量 `#166`（Byte，2~6）就是当前的「クェ」等级。

本 mod 只读这些数据：

- `mapVar[37]` → 宝藏索引 n
- `FindObjByUID(127 + n)` → 宝藏的 `pos[0], pos[1], pos[2]`
- `gEventGlobal[166]` → Kweh 等级

仅读取事件数据并绘制界面，不主动写入存档。修改过挖宝场景或启动钩子的其他 mod 需单独验证兼容性。


## 排错

如果游戏里没有出现标记，请看 `Memoria.log`，搜索 `ChocoboHotCold`：

- `Main-thread pump installed.` — 启动钩子成功
- `Version 1.0.1 ready.` — 覆盖层创建成功
- `Dig field 2950 detected (mapVar[37]=N).` — 成功识别场地与宝藏索引
- `Target #N at (x, z), distance D.` — 成功定位

如果只有前两行、没有 `Dig field ...`，通常说明游戏版本/大型改版替换了这三个场地的脚本。



## 组合使用与语言

- FloatingHPBar 与 BattleInfoOverlay 有协作初始化逻辑。单独使用 BattleInfoOverlay 时，浮窗可能要等第一次战斗指令结算后才出现。
- 从雷达 1.0.1 起，ChocoboHotCold 不再实现 `IOverloadOnBattleInitScript`，保留原有属性启动及相机、日志回调路径。已直接调用本地 Memoria 2025-07-13 的钩子选择方法，验证两种排序下血条均保留其战斗启动钩子。旧版雷达 1.0 排在血条前面时可能抢占该钩子。
- BattleInfoOverlay 的界面含固定中文；NeroNames 的名字固定为中文。其他文本取决于游戏及汉化环境。
- 不承诺与所有大型改版兼容。反馈时请附 Memoria 版本、启用顺序、复现步骤与相关日志，并先移除个人路径信息。


## 从源码构建

安装稳定版 .NET SDK 8 或更新版本。构建会读取你提供的游戏目录，只将 DLL 写入本仓库的 `ChocoboHotCold` 子目录，不安装到游戏。

Windows PowerShell（在仓库根目录）：

```powershell
./build.ps1 -GamePath 'D:/Games/FINAL FANTASY IX'
./package.ps1 -GamePath 'D:/Games/FINAL FANTASY IX'
```

macOS / Linux 的 Bash 构建入口（仅提供源码构建，运行兼容性未验证）：

```bash
bash build.sh '/path/to/FINAL FANTASY IX'
```

源码位于 `Source/ChocoboHotCold.cs`，编译产物位于 `ChocoboHotCold/StreamingAssets/Scripts/`，压缩包输出到 `dist/`。DLL 和 dist 默认不纳入 Git，成品单独上传 Releases。直接下载 GitHub 的 Source code ZIP 需要自行编译。


## 启动兼容性验证

Windows 安装 .NET Framework 4.x 和 FloatingHPBar 后，可运行：

```powershell
./tools/verify-hooks.ps1 -GamePath 'D:/Games/FINAL FANTASY IX'
```

该检查使用本地引擎的实际钩子选择方法，验证两种排序及单独启用。检查只读取启动属性元数据，不执行 Unity 回调、不启动游戏、不修改存档；不覆盖场景绘制测试。

## 作者与许可

作者：**JustPeace**。本 mod 的源码与原创内容按 [MIT License](LICENSE) 发布，允许修改、使用和再分发，须保留版权与许可声明。
FINAL FANTASY IX、Memoria 及第三方 mod 的权利归各自权利人所有。