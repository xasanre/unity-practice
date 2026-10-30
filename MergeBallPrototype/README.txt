合成大球

Unity 6000.3.26f1 · Universal 2D · Windows

试玩：完整解压 Build\MergeBall-Windows-x64.zip，然后运行其中的 MergeBall.exe（请保留同目录的数据文件与 DLL）。
自行构建后的程序：Build\Windows\MergeBall.exe。
编辑：用 Unity Hub 打开本目录，打开 Assets\Scenes\MergeBall.unity，点击 Play。

移动鼠标选择落点，在场地内单击左键投球。右上角可重新开始。
共 10 个等级，颜色与半径在 Assets\Scripts\MergeGame.cs 中统一配置。
随机投放 1～3 级；两个同级球相碰升级；10 级保留，不再合成。
合成 2～10 级分别加 2、4、8、16、32、64、128、256、512 分。
球顶超过虚线并连续 2 秒未回落，游戏结束。回落后危险时间清零。
刚投下的球有入场保护；接触球堆、进入线下或经过 0.9 秒后加入判定。
投球间隔 0.35 秒；出生点被占时不生成重叠球。
窗口初始为 540×960；调整窗口大小时保持 9:16 游戏区域。

源码：
Assets\Scripts\MergeGame.cs       落球、合成、计分、失败、重开
Assets\Scripts\MergeBall.cs       碰撞回调、入场保护
Assets\Scripts\PrototypeHud.cs    HUD 与按钮
Assets\Scripts\PortraitViewport.cs 竖屏适配
Assets\Editor\PrototypeChecks.cs  198 条规则断言
Assets\Editor\PrototypeBuilder.cs 场景创建与 Windows 构建

自检：在 Play 模式下执行 MergePrototype.PrototypeChecks.RunRules()。
重新构建：Unity 菜单 Prototype > Build Windows。
场景构建脚本仅用于首次创建，已有场景请直接编辑，避免覆盖手工调整。

本次未添加音效、存档、排行榜或联网功能。
字体来自本机：中文宋体，数字与英文 Times New Roman。

实现时核对的 Unity API：
https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rigidbody2d

验证记录（2026-10-30）
规则断言：198/198 通过。
编辑器实测：鼠标落球、重开按钮、实际碰撞合成、两步连锁、满堆超线失败、出生超时保护通过。
Windows Release 构建：成功，0 错误、0 警告。未包含 Pipeline 运行时控制监听器。
未进行长时间压力测试；球体弹性和尺寸仍可按试玩感受继续调整。
构建备注：若使用 Pipeline script eval 调试后构建出现 Illegal byte sequence，重载脚本域或重新打开项目后再构建；这是临时调试程序集引发的构建问题。
独立 EXE 实测：启动、鼠标落球、同级碰撞计分、重开、窗口最大化保持 9:16、黑边不响应落球均通过；运行日志未发现异常。
