# 怎么把这些东西传到 GitHub

> 仓库已经帮你在桌面建好，并且**已经做完第一次提交**：
> C:\Users\AMD\Desktop\史莱姆插件\github\slimes2-mods
> 你只需要「在 GitHub 建一个空仓库」→「push 上去」→「发 Release」。

---

## 先理解三件事（很重要）

1. **仓库里放什么**：只放**源码 + 说明**（已经放好了）。游戏本体的文件一个都不要进去。
2. **DLL 放哪**：不放仓库里，挂在 **Releases**（发布页）。玩家在 Releases 点一下就下载，
   而且能看到「这个版本改了什么」。
3. **Release 的附件从哪来**：六个 ZIP 已经归拢到
   C:\Users\AMD\Desktop\史莱姆插件\github\release-files\  直接拖进去就行。

---

## 路线 A：网页点几下（不碰命令行）

1. 打开 https://github.com ，登录（没账号就注册）。
2. 右上角 **＋ → New repository**：
   - **Repository name**：slimes2-mods
   - 选 **Public**（Public 才能让所有人下载）
   - 注意：**不要**勾 “Add a README file”（本地已经有了，勾了会冲突）
   - 点 **Create repository**
3. 进入空的仓库页面，点 **uploading an existing file** 链接。
4. 打开桌面的 github\slimes2-mods 文件夹，把**里面的所有内容**
   （README.md、HOW-TO-PUBLISH.md、mods、promo、.gitignore）一起拖进网页上传框，
   等它传完 → 点 **Commit changes**。
   （拖文件夹时浏览器会保留目录结构，不用担心。）
5. 发第一个 Release：仓库右侧 **Releases → Create a new release**：
   - **Choose a tag**：输入 v1.0.0，点 “Create new tag”
   - **Release title**：v1.0.0 - 首次发布
   - 说明里简单写一句改了什么
   - **Attach binaries**：把 release-files 里的 ZIP 拖进去（可全选）
   - 点 **Publish release**
6. 完成。别人访问 https://github.com/你的用户名/slimes2-mods/releases 就能下载了。

---

## 路线 B：命令行（推荐，以后更新省事）

仓库我已经 git init 并提交过了，你只差“连远程 + push”。

### 第 1 步：在 GitHub 建一个空仓库

跟路线 A 的第 2 步一样：名字 slimes2-mods、Public、**不要**勾 README。

### 第 2 步：把本地仓库接到远程并推上去

打开 PowerShell，逐行运行（把“你的用户名”换成你的 GitHub 用户名）：

    cd "C:\Users\AMD\Desktop\史莱姆插件\github\slimes2-mods"

    :: 看一眼已经有的提交
    git log --oneline

    git remote add origin https://github.com/你的用户名/slimes2-mods.git
    git branch -M main
    git push -u origin main

第一次 push 会**弹出浏览器让你登录 GitHub**（Windows 的 Git 凭据管理器），
点授权即可，以后不用再登。

### 第 3 步：发 Release

网页发（最简单）：Releases → Create a new release → 新 tag v1.0.0 → 拖入 ZIP → Publish。

装了 GitHub CLI 的话也可以一条命令搞定：

    gh release create v1.0.0 "C:\Users\AMD\Desktop\史莱姆插件\github\release-files\*.zip" --title "v1.0.0 首次发布"

### 以后更新代码

    cd "C:\Users\AMD\Desktop\史莱姆插件\github\slimes2-mods"
    git add -A
    git commit -m "MouseTweaks: 调整拖动阈值默认值"
    git push

然后照上面发一个 **v1.0.1**（新版本用新 tag，不要覆盖旧 Release）。

---

## 常见坑

| 坑 | 说明 |
|---|---|
| 把游戏文件传上去 | **绝对不要**。.gitignore 已经挡住 BepInEx/、Save/、bin/、obj/ |
| 建仓库时勾了 README | 本地再 push 会报 rejected。真发生就 git pull --rebase origin main 再 push |
| 忘了选 Public | Private 仓库别人看不到，Release 也下不了 |
| **License** | 必须**你自己决定**。GitHub 上 Add file → Create new file，文件名填 LICENSE，会弹模板选择器 |
| 国内访问慢 | GitHub 直连可能很慢，可以用 Watt Toolkit 之类的加速工具 |
| 想改仓库里的文件 | 网页点开文件 → 铅笔图标 → 改完 Commit；大批量改动还是用命令行 |

---

## 发完之后

- 把 **Release 链接**贴到 **Steam 社区指南**（Steam 每个游戏都有“指南”功能，和创意工坊无关）；
- 想发 NexusMods 的话，先去 Games → Request a game 申请给这个游戏建页（通过后再发）；
- 每个插件的**中英日三语简介**已经在 mods/<插件名>/description.md，直接复制就能用。

---

## 一个仓库放六个插件，还是一人一个仓库？

**两种都是正常做法**，取决于插件之间的关系和你的维护习惯。

| | 一个大仓库（monorepo） | 一个插件一个仓库 |
|---|---|---|
| 常见于 | **同一位作者的插件合集**、共用代码的插件 | 体量较大、独立演进的模组 |
| 好处 | 一次 push 全部更新；改公共代码方便；star/watch 集中在一个人身上 | 每个 mod 有自己的 Issues、Star、README、下载页；玩家能单独订阅 |
| 坏处 | 下载页是一个 Releases 列表，玩家要自己挑 | 六个仓库 = 六倍维护（六次 push、六处改配置） |

**NexusMods 那种“一个 mod 一个页面”的印象，来自它每个 mod 单独建页**——
GitHub 只是**文件托管 + 源码**，两边并不冲突：你完全可以在 Nexus 上开六个页面，
但它们都指向同一个 GitHub 仓库的不同下载链接。

### 对你现在这套的建议：**先一个大仓库，用“每个插件一个 tag”分开下载**

关键是让每个插件都能有**自己独立的下载链接**，这在同一个仓库里也能做到——
**每个插件发自己的 Release，tag 带插件名前缀**：

    MouseTweaks-v1.0.0
    MimicSlime-v1.0.0
    SlimeFix-v1.0.0
    BattleTweaks-v1.0.0
    ItemsTweaks-v1.0.0
    OccupyColorTweaks-v1.0.0

这样每个插件都有自己固定的页面和链接，例如：

    https://github.com/你的用户名/slimes2-mods/releases/tag/MouseTweaks-v1.0.0

发给别人时直接给这一条，他点进去只有这个插件的下载，体验和“一个 mod 一个仓库”没区别。

具体做法：Releases → Create a new release → **Choose a tag** 里输入 MouseTweaks-v1.0.0 →
**只挂 MouseTweaks 那一个 ZIP** → Publish。其余五个照做。

### 什么时候该拆成六个仓库？

- 某个插件火起来了（Issues 变多、有人提 PR），单独拆出去更清爽；
- 或者你想给不同插件写不同的 License。

**拆的时候不用重写任何东西**：把 mods/<插件名> 那个文件夹单独建一个新仓库推上去即可，
源码本来就是按插件分目录放的。

---

## 别忘了标注「AI 生成」

你已经要求给文件加上 AI 声明，仓库里已经落好了：

- **AI-DISCLOSURE.md**：完整声明（中英日）；
- **README.md 顶部**：一行醒目提示；
- **每个插件的说明**（mods/插件名/description.md）：结尾有「关于制作方式」一节；
- **每个 ZIP 里的 readme.txt**：附带同样的声明；
- **每个插件源码的第一行注释**：说明由 AI 编写。

### 发布到各平台时怎么做

| 平台 | 怎么做 |
|---|---|
| GitHub | 已经做完。README 顶部 + AI-DISCLOSURE.md + 源码头注释。建议顺便在仓库的 About 描述里也写一句 |
| **NexusMods** | **必须标**。Nexus 现在强制要求标注生成式 AI 内容，发布时在页面上勾选对应选项；说明文字里再提一句更清楚 |
| Steam 指南 | 指南正文开头写一句即可，Steam 没有专门的 AI 标签字段 |
| DLsite / 贴吧 / Discord | 帖子里写一句，别藏着 |

### 顺带说一下 License 的选择

AI 生成内容的著作权在各国的认定还不统一，**所以别用太严格的协议去主张版权**。
建议 **MIT** 或 **Unlicense / CC0** 这类宽松协议：别人能随便用、随便改，你也不用去证明版权归属。
（这条不是法律意见，只是实践建议。）
