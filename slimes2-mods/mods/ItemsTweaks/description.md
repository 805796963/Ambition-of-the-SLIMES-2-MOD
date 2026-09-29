# 道具强化 / Item Tweaks / アイテム強化

**一句话 / One line / ひとこと**
打通关奖励会发放全部道具，解除每个史莱姆的道具次数限制，变身道具对任意史莱姆可用。

---

## 中文

### 这个 mod 做什么

1. **通关奖励发放全部道具** —— 物品奖励与史莱姆奖励两条路径都会额外发放**每种道具一个**（原版只给一种）；
2. **解除道具次数限制** —— 原版每个史莱姆能用的道具次数是 `1 + 等级/10`，这里放开；
3. **变身道具对任意史莱姆可用** —— 原版只允许对杂鱼史莱姆使用；
4. **单位箱容量可调** —— 默认 32 等同于原版（即不改动），需要时再加大。

### 配置

```
[General]
Enabled    = true
VerboseLog = false

[Items]
GrantAllItemsOnItemPrize = true   # 物品奖励时发放全部道具
GrantAllItemsOnUnitPrize = true   # 史莱姆奖励时发放全部道具
UnlimitedItemUse         = true   # 解除每个史莱姆的道具次数限制
TransformAnySlime        = true   # 变身道具对任意史莱姆可用

[Units]
MaxUnitCount = 32                 # 单位箱容量，32 = 原版
```

### 安装

需要 **BepInEx 6（IL2CPP）**。把 `ItemsTweaks.dll` 放进 `<游戏>\BepInEx\plugins\`。

---

## English

1. **Every item from a stage clear** — both the item prize and the slime prize additionally grant one of every item type;
2. **No per-slime item budget** — vanilla limits each slime to `1 + level/10` item uses;
3. **The transform item works on any slime** — vanilla only allows the zako slime;
4. **Unit box size is configurable** — the default 32 is the game's own value, so it changes nothing unless you raise it.

### Config

```
[General]
Enabled    = true
VerboseLog = false

[Items]
GrantAllItemsOnItemPrize = true
GrantAllItemsOnUnitPrize = true
UnlimitedItemUse         = true
TransformAnySlime        = true

[Units]
MaxUnitCount = 32
```

**Install**: requires BepInEx 6 (IL2CPP). Drop `ItemsTweaks.dll` into `<game>\BepInEx\plugins\`.

---

## 日本語

1. **クリア報酬で全アイテム** —— アイテム報酬・スライム報酬の両方で、全種類のアイテムが1つずつ追加で手に入ります；
2. **スライムごとのアイテム回数制限を解除** —— 原版は `1 + レベル/10` 回まで；
3. **変身アイテムがどのスライムにも使える** —— 原版は雑魚スライムのみ；
4. **ユニットボックスの容量を変更可** —— 既定の 32 は原版と同じ値なので、上げない限り何も変わりません。

**導入**：BepInEx 6（IL2CPP）が必要です。`ItemsTweaks.dll` を `BepInEx\plugins\` へ。

---

## 关于制作方式 / How this was made / 制作について

**本 mod 的全部代码、说明与宣传图由 AI 助手（DeepSeek）编写。**
作者负责提出需求、决定设计取舍（例如放弃拟态动画、配色、发布方式）、在游戏内实际测试，并对发布负责。

**All code, documentation and promo art here were written by an AI assistant (DeepSeek).**
The author set the requirements, made the design calls, tested every change in the game and owns the release.

**コード・説明文・広報画像は AI アシスタント（DeepSeek）が作成しました。**
要件の提示・設計判断・ゲーム内での検証・公開は作者が担当しています。

如果你遇到问题，欢迎反馈；请知悉修复流程同样是 AI 协助的，可能需要你提供日志（BepInEx 的 LogOutput.log）才能定位。
