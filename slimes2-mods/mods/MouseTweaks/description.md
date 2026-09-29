# 鼠标操作 / Mouse Control / マウス操作

**一句话 / One line / ひとこと**
把这个**只认键盘**的游戏变成能用鼠标玩：几乎每一个界面窗口都能点，点哪选哪、点哪按哪。

---

## 中文

### 为什么这个 mod 工程量大

游戏本身**完全不接受鼠标**——所有菜单、列表、光标都只监听键盘事件。所以这个 mod 不是"把点击映射成按键"那么简单，
而是**给每个窗口单独做了一套命中判定**：

- 对**每一个会列出选项的界面**，找出"谁拥有这些选项、现在有几项、每一项在屏幕上的矩形在哪"；
- 把鼠标位置换算到游戏自己的 UI 坐标系里，命中哪一项就把**高亮游标写进那一项**，再注入一次"确认"；
- 打开的窗口会**吃掉落在它范围内的点击**——这样战场上的格子光标就不会莫名其妙地跟着窗口后面的点击乱跑。

覆盖的窗口包括：**标题菜单、确认框、消息框、对话窗、教程窗、单位窗、单位选择、道具选择、存档/读档、设置窗、
战斗命令列、部署格子，以及底部那排按键提示条**。

### 功能

- **左键单击** = 点选并确认（界面上直接生效；战场上两次快速点击算一次确认）；
- **右键** = 取消（等同 X 键）；
- **按住左键拖动** = 光标沿拖动方向扫过格子（避免一格格点）；
- **滚轮** = 上下移动选择（等同方向键）；
- 可选：滚轮在地图上东西移动；
- **只注入游戏自己的按键事件**——不写任何游戏状态、改动任何数值。所以它不会和别的 mod 打架。

### 配置

```
[General]
Enabled    = true
VerboseLog = false     # 打开后会打印每一次命中判定，调窗口用

[Mouse]
Enabled               = true
DragMovesCursor       = true
WheelNavigates        = true
WheelMovesStageSelect = false
RightClickCancels     = true
DoubleClickMs         = 350
DragThresholdPixels   = 14
DragDelayMs           = 220
```

手感不对就调后三个（双击间隔 / 拖动阈值 / 长按时延）。

### 安装

需要 **BepInEx 6（IL2CPP）**。把 `MouseTweaks.dll` 放进 `<游戏>\BepInEx\plugins\`。

---

## English

### Why this one is a lot of work

The game accepts **no mouse input at all** - every menu, list and cursor listens to keyboard events only.
So this is not a click-to-key mapping; it builds a **hit test per window**:

- for every window that lists selectable entries it resolves "who owns them, how many there are now, and
  where each entry's rectangle is on screen";
- the pointer is converted into the game's own UI space, the entry under it gets the **highlight index
  written into it**, and a confirm key event is injected;
- an open window **keeps clicks that land inside it**, so the tile cursor does not wander off behind a
  floating window.

Covered windows: **title menu, confirm dialogs, message boxes, conversation windows, tutorial windows,
the unit window, unit select, item select, save/load, the settings window, the battle action list, the
deployment grid, and the key hint bar at the bottom of the screen**.

### Features

- **Left click** = select and confirm (direct on UI; two quick clicks confirm on the field);
- **Right click** = cancel (the X key);
- **Hold left and drag** = sweep the tile cursor along the drag instead of stepping tile by tile;
- **Wheel** = move the selection up and down (the arrow keys);
- Optional: the wheel walks the world map east and west;
- **Only the game's own key events are injected** - no game state or stat is written, so it cannot
  conflict with other mods.

### Config

```
[General]
Enabled    = true
VerboseLog = false     # logs every hit test, for tuning window coverage

[Mouse]
Enabled               = true
DragMovesCursor       = true
WheelNavigates        = true
WheelMovesStageSelect = false
RightClickCancels     = true
DoubleClickMs         = 350
DragThresholdPixels   = 14
DragDelayMs           = 220
```

**Install**: requires BepInEx 6 (IL2CPP). Drop `MouseTweaks.dll` into `<game>\BepInEx\plugins\`.

---

## 日本語

### なぜ手間がかかっているか

このゲームは**マウス入力を一切受け付けません**（メニュー・リスト・カーソルはすべてキーボードイベントのみ）。
したがってこの mod は「クリックをキーに割り当てる」ものではなく、**ウィンドウごとに当たり判定を組み立てます**：

- 選択項目を持つ各ウィンドウについて「持ち主・現在の項目数・各項目の画面上の矩形」を解決し；
- ポインタをゲーム自身の UI 座標に変換し、その項目に**ハイライト位置を書き込み**、決定キーイベントを注入；
- 開いているウィンドウは**内部のクリックを消費**するので、戦場のタイルカーソルが背後で勝手に動きません。

対応ウィンドウ：**タイトル、確認ダイアログ、メッセージ、会話、チュートリアル、ユニット画面、ユニット選択、
アイテム選択、セーブ/ロード、設定、戦闘コマンド列、配置グリッド、画面下部のキー案内バー**。

### 機能

- **左クリック** = 選択して決定（UI では直接、戦場では素早い2回クリックで決定）；
- **右クリック** = キャンセル（X キー相当）；
- **左ボタンを押したままドラッグ** = カーソルがマスを掃くように移動；
- **ホイール** = 選択の上下移動（方向キー相当）；
- 任意でワールドマップの東西移動；
- **ゲーム自身のキーイベントを注入するだけ**で、状態や数値は一切書き換えません。

**導入**：BepInEx 6（IL2CPP）が必要です。`MouseTweaks.dll` を `BepInEx\plugins\` へ。

---

## 关于制作方式 / How this was made / 制作について

**本 mod 的全部代码、说明与宣传图由 AI 助手（DeepSeek）编写。**
作者负责提出需求、决定设计取舍（例如放弃拟态动画、配色、发布方式）、在游戏内实际测试，并对发布负责。

**All code, documentation and promo art here were written by an AI assistant (DeepSeek).**
The author set the requirements, made the design calls, tested every change in the game and owns the release.

**コード・説明文・広報画像は AI アシスタント（DeepSeek）が作成しました。**
要件の提示・設計判断・ゲーム内での検証・公開は作者が担当しています。

如果你遇到问题，欢迎反馈；请知悉修复流程同样是 AI 协助的，可能需要你提供日志（BepInEx 的 LogOutput.log）才能定位。
