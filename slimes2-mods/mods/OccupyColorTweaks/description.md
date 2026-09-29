# 占领解放 / Occupy Tweaks / 憑依解放

**一句话 / One line / ひとこと**
解除占领限制（护盾机制保留），并修复强制占领后的显示。

---

## 中文

### 这个 mod 做什么

1. **解除占领限制** —— 原本不能占领的单位也可以占领（`IsAblePossessionUnit` 与 `CheckOccupyResult` 两道判定都放开）；
2. **保留护盾机制** —— 身上有护盾的单位仍然无法被占领（可开关）；
3. **修复强制占领后的显示** —— 强制占领后单位有时会显示成空白/碎图，这里让贴图查找回退到游戏自带的 `_m` / `_face` 版本，并重绘一次；
4. **阵营染色（可选，默认关）** —— 给强制占领的单位上一层阵营色，参数可调（红/绿/蓝/亮度/混合强度）。

**只做显示，不写占领状态、阵营、技能或数值。**

### 配置

```
[General]
Enabled    = true
VerboseLog = false

[Occupy]
PreserveShield  = true    # 有护盾时仍不可占领
ForceTargetable = true    # 放开占领判定

[Display]
ShowForcedPossession = true
FactionTint          = false   # 阵营染色开关
TintRed/TintGreen/TintBlue/TintGlow/TintStrength
```

### 安装

需要 **BepInEx 6（IL2CPP）**。把 `OccupyColorTweaks.dll` 放进 `<游戏>\BepInEx\plugins\`。

---

## English

1. **Occupation restrictions lifted** — units the game refuses to let you possess can be possessed (`IsAblePossessionUnit` and `CheckOccupyResult` are both opened);
2. **Shield mechanic preserved** — a unit with a shield still cannot be possessed (switchable);
3. **Forced possession is visible again** — the atlas lookup falls back to the game's own `_m` / `_face` art and the quad is repainted once;
4. **Optional faction tint (off by default)** — recolours a forced possession towards the faction look, with tunable channels, glow and blend strength.

**Rendering only: no occupation state, side type, ability or stat is written.**

**Install**: requires BepInEx 6 (IL2CPP). Drop `OccupyColorTweaks.dll` into `<game>\BepInEx\plugins\`.

---

## 日本語

1. **憑依制限の解除** —— 本来憑依できないユニットにも憑依できます；
2. **シールド機構は維持** —— シールド持ちは依然として憑依不可（切り替え可）；
3. **強制憑依後の表示を修正** —— アトラス検索をゲーム標準の `_m` / `_face` にフォールバックし、一度描き直します；
4. **陣営カラー（任意・既定オフ）** —— 赤/緑/青/輝度/ブレンド強度を調整可能。

**描画のみ。憑依状態・陣営・スキル・数値は書き換えません。**

**導入**：BepInEx 6（IL2CPP）が必要です。`OccupyColorTweaks.dll` を `BepInEx\plugins\` へ。

---

## 关于制作方式 / How this was made / 制作について

**本 mod 的全部代码、说明与宣传图由 AI 助手（DeepSeek）编写。**
作者负责提出需求、决定设计取舍（例如放弃拟态动画、配色、发布方式）、在游戏内实际测试，并对发布负责。

**All code, documentation and promo art here were written by an AI assistant (DeepSeek).**
The author set the requirements, made the design calls, tested every change in the game and owns the release.

**コード・説明文・広報画像は AI アシスタント（DeepSeek）が作成しました。**
要件の提示・設計判断・ゲーム内での検証・公開は作者が担当しています。

如果你遇到问题，欢迎反馈；请知悉修复流程同样是 AI 协助的，可能需要你提供日志（BepInEx 的 LogOutput.log）才能定位。
