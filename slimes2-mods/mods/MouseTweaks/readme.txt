# 鼠标操作 / Mouse Control / マウス操作

## 中文
把 MouseTweaks.dll 放进 <游戏目录>\BepInEx\plugins\，需要 BepInEx 6（IL2CPP）。
配置：BepInEx\config\dsh.ambition.slimes2.mousetweaks.cfg。
给这个只认键盘的游戏加一整套鼠标命中判定：标题/确认/消息/对话/教程/单位/单位选择/道具选择/存档/设置/
战斗命令列/部署格子/底部提示条都能点；支持点击确认、右键取消、拖动扫格、滚轮选择。
只注入游戏自己的按键事件，不写任何游戏状态。

## English
Drop MouseTweaks.dll into <game>\BepInEx\plugins\. Requires BepInEx 6 (IL2CPP).
Config: BepInEx\config\dsh.ambition.slimes2.mousetweaks.cfg.
A full pointer layer for a keyboard-only game: hit tests for the title, confirm, message, conversation,
tutorial, unit, unit select, item select, save/load, settings, battle action list, deployment grid and the
bottom hint bar. Click to confirm, right click to cancel, drag to sweep, wheel to navigate.
Only the game's own key events are injected; no game state is written.

---
AI 声明：本插件的代码与说明由 AI 助手（DeepSeek）编写；需求、设计取舍、游戏内测试与发布由作者完成。
AI notice: all code and documentation were written by an AI assistant (DeepSeek); the author set the
requirements, tested everything in the game and owns the release. / コードと説明は AI アシスタントが作成。
