# 史莱姆大修 / Slime Fix / スライム大修

**一句话 / One line / ひとこと**
给《史莱姆的大野望》的史莱姆加了五处改动：继承次数、属性附身、掉落本体、消化出骷髅、分裂两代。

---

## 中文

### 这个 mod 做什么

1. **继承次数 999** —— 拥有「继承」被动的赫尔墨斯史莱姆，技能次数从 1 提到 999（战斗结束后仍只携带 1 只，这是游戏自身规则；999 只是让你在一场战斗里不用省）。**其他史莱姆一律不动。**

2. **太阳属性附身改属性** —— 太阳属性的史莱姆附身敌人后，被附身者的属性也会变成太阳（方便打克制）。

3. **杂鱼史莱姆掉落本体** —— 杂鱼附身的宿主被打死时，它不会跟着一起死，而是掉落自己的本体站在同一格，可以**再附身一次**。**每场战斗仅一次**；下一发致命伤就正常死亡。只影响玩家自己的史莱姆。

4. **溶解史莱姆「消化」** —— 吞下敌人后，选择框会多出一格「消化」：消耗 1 次技能次数，把肚子里的单位变成**同等级、自己阵营的骷髅**，生成在史莱姆所在格，然后史莱姆退回普通状态。

5. **分裂两代** —— 分裂史莱姆可以分裂两次（本体 → 子实体 → 孙代），孙代不能再分裂，且**以 20% 透明度**显示以示区别；血量低于 20% 时禁止分裂。

### 配置

```
[General]
Enabled     = true
VerboseLog  = false    # 出问题时可开，会写详细日志

[Inherit]
UseCount     = 999     # 继承次数
SourceType   = 0       # 0 = 自动识别持有「继承」的史莱姆

[PossessAttr]
SlimeAttr    = 4       # 4 = 太阳
GrantAttr    = 4

[ReviveOnDeath]
SlimeType    = 1       # 1 = 杂鱼史莱姆

[Digest]
SlimeType     = 10     # 10 = 溶溶史莱姆
SkeletonType  = 62     # 62 = 骷髅
CopyAttribute = true

[Divide]
ChildSplits      = 1
MinHpPercent     = 20
CanSplitAlpha    = 1
CannotSplitAlpha = 0.8  # 分不了的单位 = 20% 透明
```

### 安装

1. 游戏装 **BepInEx 6（IL2CPP，bleeding-edge 构建）**；
2. 把 `SlimeFix.dll` 放进 `<游戏目录>\BepInEx\plugins\`；
3. 启动一次游戏，配置会生成在 `BepInEx\config\`。

### 说明

- 只改运行时的数据与流程，**不替换任何资源文件**，可随时删除；
- 「继承 999」这个数字在界面上显示不出来（游戏只有 0–9 的数字图），但效果是实实在在的。

---

## English

### What it does

1. **Inherit uses: 999** — the Hermes slime that owns the `继承` (inherit) passive gets 999 ability uses instead of 1. Only that one slime type is touched. (It still carries one unit after a battle; that part is the game's own rule.)

2. **Sun-attribute possession changes the host's attribute** — possess an enemy with a sun slime and the possessed unit becomes sun too.

3. **Zako slime drops its own body** — when the host a zako is possessing is killed, the zako survives: it drops its body on the same cell and may **possess once more**. **Once per battle**; the next lethal hit kills it normally. Player-owned slimes only.

4. **Melty slime "digest"** — after swallowing a unit, the choice window gains a third entry. It spends one ability use, destroys the swallowed unit, and puts a **same-level skeleton on your side** on the slime's cell. The slime returns to its normal state.

5. **Two generations of splitting** — a split slime may split twice (parent → child → grandchild). The grandchild cannot split again and is drawn at 20% opacity to show it. Splitting is refused below 20% HP.

### Install

Requires **BepInEx 6 (IL2CPP, bleeding-edge)**. Drop `SlimeFix.dll` into `<game>\BepInEx\plugins\` and run the game once to generate the config.

---

## 日本語

### 内容

1. **継承回数を 999 に** —— 「継承」を持つヘルメススライムのスキル回数を 1 から 999 に。**他のスライムには一切影響しません。**

2. **太陽属性の憑依で属性も変化** —— 太陽スライムが憑依した相手の属性も太陽になります。

3. **雑魚スライムが本体を落とす** —— 憑依先が倒されても雑魚は死なず、同じマスに本体を落として**もう一度憑依**できます。**1戦闘につき1回**、次の致命打で普通に死亡。プレイヤー側のスライムのみ。

4. **溶けるスライムの「消化」** —— 敵を飲み込んだ後の選択肢に3つ目が増えます。スキル回数を1消費し、飲み込んだユニットを**同レベルの味方スケルトン**に変えて、スライムのマスに出現させます。

5. **分裂は2世代まで** —— 親 → 子 → 孫。孫はもう分裂できず、区別のため**不透明度 20%** で描画されます。HP 20% 未満では分裂できません。

### 導入

**BepInEx 6（IL2CPP、bleeding-edge）** が必要です。`SlimeFix.dll` を `<ゲーム>\BepInEx\plugins\` に入れて一度起動してください。

---

## 关于制作方式 / How this was made / 制作について

**本 mod 的全部代码、说明与宣传图由 AI 助手（DeepSeek）编写。**
作者负责提出需求、决定设计取舍（例如放弃拟态动画、配色、发布方式）、在游戏内实际测试，并对发布负责。

**All code, documentation and promo art here were written by an AI assistant (DeepSeek).**
The author set the requirements, made the design calls, tested every change in the game and owns the release.

**コード・説明文・広報画像は AI アシスタント（DeepSeek）が作成しました。**
要件の提示・設計判断・ゲーム内での検証・公開は作者が担当しています。

如果你遇到问题，欢迎反馈；请知悉修复流程同样是 AI 协助的，可能需要你提供日志（BepInEx 的 LogOutput.log）才能定位。
