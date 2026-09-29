# 百变史莱姆 / Mimic Slime / 百変スライム

**一句话 / One line / ひとこと**
杂鱼史莱姆获得「拟态」：可以变成场上任意单位，以对方 70% 的等级战斗到本场结束。

---

## 中文

### 这个 mod 做什么

杂鱼史莱姆多出一个技能「**拟态**」。选定场上**任意单位**（史莱姆或人类、敌我皆可）后：

- 变成对方的**样子与技能**（技能、射程、范围、目标类型整套继承）；
- 等级为对方的 **70%**（HP / 攻击 / 防御由等级自动派生）；
- **本场战斗一直保持**这个形态，直到被打死；
- 拟态成**非史莱姆**时自动带**被附身**状态（方便识别这是我方）；
- 拟态体会被染成**绿色**以便区分。

限制：**每场战斗只能拟态一次**；**不能拟态分裂史莱姆**（否则复制品也能分裂，太强）。

### 配置

```
[General]
Enabled    = true
VerboseLog = false

[Mimic]
SlimeType           = 1      # 1 = 杂鱼史莱姆
ShellType           = 15     # 借用 15 = 破魔史莱姆 的技能作为外壳
LevelPercent        = 70     # 对方等级的 70%
ShieldBreakPercent  = 5      # 外壳自带的破盾，保留 5% 触发
ReplaceCaster       = true   # 本体消失、形态顶替
ForbidTypes         = 21,22  # 禁止拟态的类型（分裂史莱姆）
TintR / TintG / TintB = 0.25 / 1 / 0.25
```

### 已知问题

- 拟态体**没有待机动画**（静止贴图）。这一条是**有意保留**的：复制品本来就该是僵硬的；
- 死亡后会变回原本的史莱姆——这是游戏的队伍/箱子规则使然，实际观感不错。

### 安装

需要 **BepInEx 6（IL2CPP）**。把 `MimicSlime.dll` 放进 `BepInEx\plugins\`。

---

## English

### What it does

The zako slime gains a **Mimic** ability. Pick **any unit on the field** (slime or human, yours or theirs) and it:

- takes that unit's **shape and skills** (the ability, its length, range and target type are copied whole);
- ends up at **70% of that unit's level** (HP, attack and defence follow from the level);
- **keeps the shape for the rest of the battle**, until it dies;
- is marked as a **possessed body** when the shape is not a slime, so it reads as yours;
- is tinted **green** so you can tell it apart.

Limits: **once per battle**, and the **split slimes cannot be copied** (a copy that could split would be far too strong).

### Known limitations

- The shape has **no idle animation** (a static sprite). This is **intentional** - a copy should look stiff.
- It reverts to the original slime after it dies; that comes from the game's own roster rules and looks fine in practice.

### Install

Requires **BepInEx 6 (IL2CPP)**. Drop `MimicSlime.dll` into `BepInEx\plugins\`.

---

## 日本語

### 内容

雑魚スライムが「**擬態**」を習得します。フィールド上の**任意のユニット**（スライム／人間、敵味方問わず）を選ぶと：

- 相手の**見た目とスキル**を丸ごとコピー（スキル・射程・範囲・対象タイプすべて）；
- レベルは相手の **70%**（HP・攻撃・防御はレベルから自動算出）；
- **その戦闘の間ずっと**その姿を維持（倒されるまで）；
- スライム以外に擬態した場合は自動で**憑依状態**になり、味方だと分かります；
- 見分けやすいよう**緑色**に染まります。

制限：**1戦闘につき1回のみ**、**分裂スライムには擬態できません**（コピーが分裂できると強すぎるため）。

### 既知の制限

- 擬態体には**待機モーションがありません**（静止画）。これは**意図的な仕様**です。コピーらしく硬いままで。
- 倒されると元のスライムに戻ります（ゲーム側の名簿処理によるもので、見た目は自然です）。

### 導入

**BepInEx 6（IL2CPP）** が必要です。`MimicSlime.dll` を `BepInEx\plugins\` に入れてください。

---

## 关于制作方式 / How this was made / 制作について

**本 mod 的全部代码、说明与宣传图由 AI 助手（DeepSeek）编写。**
作者负责提出需求、决定设计取舍（例如放弃拟态动画、配色、发布方式）、在游戏内实际测试，并对发布负责。

**All code, documentation and promo art here were written by an AI assistant (DeepSeek).**
The author set the requirements, made the design calls, tested every change in the game and owns the release.

**コード・説明文・広報画像は AI アシスタント（DeepSeek）が作成しました。**
要件の提示・設計判断・ゲーム内での検証・公開は作者が担当しています。

如果你遇到问题，欢迎反馈；请知悉修复流程同样是 AI 协助的，可能需要你提供日志（BepInEx 的 LogOutput.log）才能定位。
