# KeyboundTab · 魔光键影 Tab 补全

手动按快捷键填入一个当前词条，由玩家自己按 Enter 发送。

当前正式版为 **1.0.0**，基于已验收的 0.1.5 补全功能，适配国服游戏版本 `2026.10.06.0000.0000`。已通过游戏采集核实中文词条、动态词条组件和输入框焦点，维护者已确认快捷键填词生效。读条词条采用加权随机，保留绿墙等其他可输入词条；不同机制下的禁输入、闲置判定和输入法边界仍持续接受游戏内反馈。未适配的游戏版本会自动关闭补全。

![KeyboundTab 图标](icon.png)

## 使用

1. 在 `/xlsettings` → Experimental 的额外插件仓库中添加：

   ```text
   https://raw.githubusercontent.com/endfish/DalamudPlugins/main/repo.json
   ```

2. 保存设置，打开 `/xlplugins`。
3. 搜索并安装“魔光键影 Tab 补全”。原有开发版与正式版使用相同的 `InternalName: KeyboundTab`，启用其中一个即可。
4. 聚焦小游戏输入框，按 Tab 填词，再自行按 Enter 发送；输入法组词期间先完成或取消组词。

`/kbt` 打开设置。可选择 Tab、Space、F1–F12、Home、End、Insert，支持 Ctrl/Alt/Shift 组合；Enter、Escape 和系统 Alt+Tab/Alt+F4 不可绑定。关闭设置窗口后再使用补全。

`/kbt on`、`/kbt off` 启停补全，`/kbt status` 查看状态。`/kbt debug` 开关本地采集，`/kbt dump` 立即采集当前界面。

采集保存在 Dalamud 插件配置目录的 `KeyboundTab/keybound-probe.jsonl`。包含小游戏输入、词条、施法条、血条和任务目标，以及相关敌人的共享界面数据与只读闲置计时；不采集聊天、玩家名字界面或 Content ID。自动采集省略隐藏模板和空节点；文件按 16 MiB 轮换，最多保留当前与前一份；不上传。

## 补全行为

- 空输入随机选一个当前可输入词条。
- 已输入部分内容时优先匹配前缀；无匹配则随机替换。
- 保留上次填入内容时再次按键换选：先选本轮尚未预览的当前词条；多个词条时不立即重复刚填入的一条。
- 默认给读条词条两份抽签机会，其他词条一份，全部保留。可在设置中关闭加权。手动前缀仍优先匹配，不强制切换你的目标。
- 输入框被禁用、被其他界面阻挡时不改写输入；尚未识别的机制不以猜测状态强行绕过。
- 长按只触发一次，Enter 始终由玩家决定。
- 没有有效候选、输入框失焦、切出游戏、输入法组词或打开插件设置时不接管按键。
- 只监听真实快捷键，不处理定型文、不拦截或清除游戏按键状态。
- 不自动按键，不修改闲置计时器。用户报告过仅用补全时被判闲置，当前已移除清按键与输入拦截，正在通过只读计时记录核实修复效果。

## 构建与验证

需要 .NET SDK 10、Dalamud.NET.Sdk 15 和本机 Dalamud API 15 开发程序集。`DALAMUD_HOME` 可指定开发程序集目录；默认使用当前用户的 XIVLauncherCN 开发目录。

```powershell
dotnet build KeyboundTab.csproj -c Debug -p:OutputPath=artifacts/build/
dotnet build KeyboundTab.csproj -c Release -p:OutputPath=artifacts/release/
dotnet run --project Tests/KeyboundTab.Tests.csproj
```

先构建到独立目录并完成检查，再将 DLL 更新到已登记的开发插件路径以触发重载。自动化验证覆盖选择、前缀、重选、按键边沿、修饰键、输入长度和可见词条筛选，不能替代完整游戏内验收。

插件运行时直接读取游戏自身词表，无需另装输入法词库。仓库及插件安装包不包含游戏二进制、词表导出或本机采集记录。
