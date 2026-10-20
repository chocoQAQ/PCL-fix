# PCL 修复版

本项目是 **chocoQAQ 基于 Plain Craft Launcher（PCL）独立维护的第三方修改版**，恢复上游 2.99.0.0 中关闭的 Minecraft 原版下载及相关整合包安装入口。原作开发者为 **龙腾猫跃**，感谢原作者与社区贡献者。

本次修复、构建验证及自动同步配置由 AI 编程助手 Codex 辅助完成，详见 [AI 工作说明](AI_WORK.md)。

- [下载修复版](https://github.com/chocoQAQ/PCL-fix/releases/latest)
- [构建记录](https://github.com/chocoQAQ/PCL-fix/actions/workflows/build.yml)
- [上游仓库](https://github.com/Meloong-Git/PCL)
- [赞助原作者](https://meloong.com/afd/a/LTCat)

## 使用

从 Releases 下载 `PCL-fix-<commit>.exe`，直接运行即可。需要 Windows 与 .NET Framework 4.8；普通用户不需要安装 SDK 或 Visual Studio。

修复版沿用上游官方源与镜像源机制、文件校验和正版购买及赞助提示。配置的注册表位置为 `HKEY_CURRENT_USER\Software\PCLFix`，与原版隔离。建议放在单独目录中使用。

“检查更新”会打开本仓库的 Releases 页面，由用户下载更新，不会安装官方发行文件。EXE 文件名包含源码提交号，Release 说明中提供 SHA-256 校验值。

微软正版登录已接入本项目注册的公开 Client ID，普通用户无需自行填写。登录通过微软官方网页授权，启动器继续验证 Xbox 身份、Minecraft 游戏资格和玩家档案；刷新登录使用同一微软 v2 接口。能申请登录代码不代表 Minecraft API 审核已获批，服务拒绝应用时会明确提示审核或配置问题。维护者可在启动前用环境变量 `PCL_MS_CLIENT_ID` 覆盖应用编号；桌面公共客户端不需要 Client Secret。

**2026-10-20 实测状态：** Microsoft 网页授权、Xbox Live 和 XSTS 已通过；Minecraft 接口返回 `Invalid app registration`，此应用编号尚未被 Minecraft 服务放行。因此当前仍无法完成正版登录，需等待对应 Client ID 的 Minecraft API 审核获批并生效后再验证。游戏资格、玩家档案和自动刷新登录尚未完成真人实测。

主题选择、自定义主题、隐藏主题解锁及土豆码入口已移除，保留默认蓝色界面、背景图片和透明度等设置。

CurseForge API 尚未在此版本配置，仍需环境变量 `PCL_CURSEFORGE_API_KEY`。本仓库不内置或借用其他项目的私有凭据。原版 Minecraft 下载不依赖上述 API 凭据。

## 本地构建与验证

安装 Visual Studio 2022 17.12 或更新版本的 .NET 桌面开发/构建工具、.NET Framework 4.8 Targeting Pack、.NET SDK 9 和 PowerShell 7。`global.json` 选择兼容 VS 2022 的 .NET 9 SDK；应用仍面向 .NET Framework 4.8。

```powershell
git clone --recurse-submodules https://github.com/chocoQAQ/PCL-fix.git
cd PCL-fix
pwsh -File scripts/Build-Fix.ps1
pwsh -File scripts/Test-MicrosoftLogin.ps1
pwsh -File scripts/Test-Download.ps1
```

构建结果位于 `artifacts/PCL-fix-<commit>.exe`。如需指定 MSBuild，可使用 `-MSBuildPath '完整的 MSBuild.exe 路径'`。

下载验证程序加载编译后 EXE 内嵌的实际依赖，检查成品中的下载限制开关，使用启动器的下载器获取 Mojang 版本列表、Minecraft 1.21.1 JSON、完整客户端 JAR、资源索引及抽样支持库和资源，并校验文件哈希。测试不需要游戏账号，文件和缓存均在 `artifacts/download-test`。它验证下载和安装，不代表已完成登录或游戏启动测试。

微软协议回归检查验证成品中的轮询间隔、等待授权、限流退避、代码过期、取消、凭据缺失及错误提示，无需账号且不访问登录服务。需要真人配合时，可运行以下命令，按终端给出的官方网页和代码授权；测试会验证 Xbox、Minecraft 正版资格、玩家档案和令牌刷新。登录令牌仅留在进程内存中，结果文件只记录是否通过，不写入启动器账号配置。

```powershell
pwsh -File scripts/Test-MicrosoftLogin.ps1 -Interactive
```

```powershell
# 额外执行完整原版文件安装，下载约 1 GB；不启动游戏
pwsh -File scripts/Test-Download.ps1 -FullInstall
```

## 自动同步和构建

`Sync upstream and build` 每 6 小时（UTC 00:23、06:23、12:23、18:23）检查 `Meloong-Git/PCL:main`，也支持手动运行。

同步使用普通 Git merge 保留修复提交，检查下载开关、Client ID、默认界面、第三方标识和更新地址，再执行 Release 构建、微软登录协议回归检查及实际下载验证。全部通过后才推送到 `main` 并触发发布，将 EXE 上传到 Releases。定时工作流不会申请登录代码或使用个人账号。

冲突、上游接口变化、构建或下载检查失败时，工作流失败且不推送未经验证的合并。查看 Actions 失败日志并修复后重新运行；上游不更新时不会重复构建。上游源码的同步频率由原作者决定。

Fork 的工作流可能默认关闭：在 Actions 页面启用工作流，手动运行 `Build PCL-fix` 和 `Sync upstream and build` 各一次。需允许工作流的 `contents: write`（推送和发布）及 `actions: write`（触发构建）权限；无需额外 PAT 或仓库 Secret。主分支若设置了强制 PR 保护，则应相应调整同步流程。GitHub 可能暂停长期无活动公共仓库的定时工作流，届时需重新启用。

## 使用条款

保留并继续遵循上游 [LICENCE](LICENCE) 与其中引用的《PCL 分发有限许可》。本仓库的第三方标识、作者署名和赞助链接不代表原作者为修改版背书。Minecraft 游戏文件不随修复版分发，而由用户运行启动器时从下载源获取。
