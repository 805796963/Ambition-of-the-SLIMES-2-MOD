# 老牌战斗 / Old School Battle / 旧作風バトル

**一句话 / One line / ひとこと**
让所有单位造成与受到的伤害翻倍，战斗更快更疼。

---

## 中文

这个 mod 只做一件事：**伤害 ×2**。

游戏里「造成的伤害」和「受到的伤害」用的是**同一套计算**，所以在这里放大就是**双向翻倍**；而**固定防御减伤不跟着放大**——这正是伤害数字变大、战斗节奏变快的原因，也就是"老牌战斗"的手感。

- 攻击与反击都生效；
- 想微调就改 `Multiplier`：`1` = 关闭效果，`1.5` = 温和，`3` = 疯狂；
- 只改伤害计算的结果，不碰 AI、不碰数值成长。

```
[General]
Enabled    = true
VerboseLog = false

[Damage]
Multiplier = 2
```

**安装**：需要 BepInEx 6（IL2CPP），把 `BattleTweaks.dll` 放进 `BepInEx\plugins\`。

---

## English

This mod does exactly one thing: **damage ×2**.

The game resolves damage given and damage taken with the **same calculation**, so scaling it here doubles **both directions**, while the game's **flat defence subtraction is not scaled** - that is what makes the numbers bigger and the fights resolve faster, the "old school" feel.

- Applies to attacks and counterattacks;
- Tune it with `Multiplier`: `1` disables the effect, `1.5` is mild, `3` is chaos;
- Only the damage result is touched: no AI changes, no growth changes.

```
[General]
Enabled    = true
VerboseLog = false

[Damage]
Multiplier = 2
```

**Install**: requires BepInEx 6 (IL2CPP). Drop `BattleTweaks.dll` into `BepInEx\plugins\`.

---

## 日本語

やることは一つ、**ダメージ 2 倍**です。

このゲームは「与えるダメージ」と「受けるダメージ」を**同じ計算**で処理するため、ここを倍にすると**両方向が倍**になります。一方で**固定の防御減算は倍にならない** —— これが数字が大きくなり、戦闘が速く決着する「旧作風」の理由です。

- 攻撃も反撃も対象；
- `Multiplier` で調整可：`1` で無効、`1.5` で控えめ、`3` でカオス；
- 変えるのはダメージ計算の結果だけ（AI や成長には触りません）。

```
[General]
Enabled    = true
VerboseLog = false

[Damage]
Multiplier = 2
```

**導入**：BepInEx 6（IL2CPP）が必要です。`BattleTweaks.dll` を `BepInEx\plugins\` へ。

---

## 关于制作方式 / How this was made / 制作について

**本 mod 的全部代码、说明与宣传图由 AI 助手（DeepSeek）编写。**
作者负责提出需求、决定设计取舍（例如放弃拟态动画、配色、发布方式）、在游戏内实际测试，并对发布负责。

**All code, documentation and promo art here were written by an AI assistant (DeepSeek).**
The author set the requirements, made the design calls, tested every change in the game and owns the release.

**コード・説明文・広報画像は AI アシスタント（DeepSeek）が作成しました。**
要件の提示・設計判断・ゲーム内での検証・公開は作者が担当しています。

如果你遇到问题，欢迎反馈；请知悉修复流程同样是 AI 协助的，可能需要你提供日志（BepInEx 的 LogOutput.log）才能定位。
