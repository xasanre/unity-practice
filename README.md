# Unity Practice

Unity 游戏练习合集，包含 AngryBirdsReplica、完整版 TowerDefense 和合成大球玩法原型。后续 FPS 项目也会放在这个仓库中，目前尚未上传。

| 工程目录 | Unity 版本 | 入口场景 |
| --- | --- | --- |
| `AngryBirdsReplica/` | 2022.3.62f3c1 | `Assets/Level01.unity` |
| `TowerDefense/` | 6000.3.26f1 | `Assets/Scenes/Level01.unity` |
| `MergeBallPrototype/` | 6000.3.26f1 | `Assets/Scenes/MergeBall.unity` |

## 打开工程

1. 克隆或下载本仓库。
2. 在 Unity Hub 中添加需要打开的子目录：`AngryBirdsReplica`、`TowerDefense` 或 `MergeBallPrototype`。不要把仓库根目录当作 Unity 工程打开。
3. 使用上表对应的 Unity 版本，等待资源导入和编译完成。
4. 打开入口场景，点击 Play。

仓库包含各工程的 `Assets`、`Packages`、`ProjectSettings` 和资源 `.meta` 文件，不包含 `Library` 等生成缓存。合成大球另附 Windows 试玩 ZIP；其余工程未附可执行构建。AngryBirdsReplica 原有 `Readme.txt` 中的 `run` 文件夹说明仅适用于另外导出的游戏包。

## AngryBirdsReplica

- 鼠标左键拖动红色小鸟，向后拉伸后松开即可发射。
- 两个关卡，包含木质与石质结构、碰撞消灭敌人和粒子效果。
- 每关五只小鸟；胜利后点击 Next Level，点击 Retry 重试。

## TowerDefense

这里保存的是完整版，不是 Basic 基础版。

- 初始 200 金币、5 点生命，提供六个固定建造位置。
- 选择 Standard（100 金币）或 Heavy（150 金币），再左键点击空建造点。
- 至少建造一座炮塔后点击 Start Waves，三波敌人自动依次出现。
- 炮塔自动追踪和攻击敌人，每次击杀获得 15 金币，可在战斗中继续建造。
- 包含两种炮塔、金币系统、递增的敌人难度、胜利／失败弹窗和重新开始功能。
- 点击重开按钮或按 R 重新开始。
- 编辑器 Play 模式下，可在 GameManager 组件中调整 Test Speed（1–10）加速测试；此选项仅在编辑器中生效。

## 后续添加项目

将新的 FPS 工程作为独立子目录放在仓库根目录。保留其 `Assets`、`Packages`、`ProjectSettings` 和 `.meta` 文件，不提交 `Library` 或构建输出。

## MergeBallPrototype

[下载 Windows 64 位试玩包](https://github.com/xasanre/unity-practice/raw/refs/heads/main/MergeBallPrototype/Build/MergeBall-Windows-x64.zip)（约 49 MB）。完整解压后运行 `MergeBall.exe`，不要单独移动 EXE 或删除同目录的数据文件和 DLL。

- 9:16 竖屏；移动鼠标选择落点，在场地内单击左键投球。
- 十级 2D 圆球，仅用颜色和大小区分；同级碰撞合成，十级保留。
- 随机投放 1～3 级球；合成 2～10 级分别得到 2～512 分，逐级翻倍。
- 球堆超过顶部虚线连续 2 秒失败，回落则清零危险计时；提供出生保护和重开按钮。
- 自检入口：Play 模式调用 `MergePrototype.PrototypeChecks.RunRules()`，包含 198 条规则断言。
- Windows 构建：Unity 菜单 `Prototype > Build Windows`；已打包的试玩版本位于 `MergeBallPrototype/Build/MergeBall-Windows-x64.zip`，不包含 `DoNotShip` 调试文件。
