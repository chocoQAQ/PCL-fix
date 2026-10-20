# PCL 下载修复版

本项目是 **chocoQAQ 基于 Plain Craft Launcher（PCL）独立维护的第三方修改版**，恢复上游 2.99.0.0 中关闭的 Minecraft 原版下载及相关整合包安装入口。原作开发者为 **龙腾猫跃**，感谢原作者与社区贡献者。

- [下载修复版](https://github.com/chocoQAQ/PCL-fix/releases/latest)
- [构建记录](https://github.com/chocoQAQ/PCL-fix/actions/workflows/build.yml)
- [上游仓库](https://github.com/Meloong-Git/PCL)
- [赞助原作者](https://meloong.com/afd/a/LTCat)

## 使用

从 Releases 下载 `PCL-fix-<commit>.zip`，完整解压后运行 `Plain Craft Launcher 2.exe`。需要 Windows 与 .NET Framework 4.8；普通用户不需要安装 SDK 或 Visual Studio。

修复版沿用上游官方源与镜像源机制、文件校验和正版购买及赞助提示。配置的注册表位置为 `HKEY_CURRENT_USER\Software\PCLFix`，与原版隔离。建议放在单独目录中使用。

“检查更新”会打开本仓库的 Releases 页面，由用户下载更新，不会安装官方发行文件。发行包包含源码提交号与 SHA-256 校验文件。

公开源码不包含官方私有凭据：微软登录需要自行申请并在启动前设置进程或用户环境变量 `PCL_MS_CLIENT_ID`；CurseForge API 需要 `PCL_CURSEFORGE_API_KEY`。本仓库不内置、借用或收集其他项目的凭据。原版 Minecraft 下载不依赖这些凭据。主题解锁等上游未公开实现仍沿用源码版行为。

## 本地构建与验证

安装 Visual Studio 2022 17.12 或更新版本的 .NET 桌面开发/构建工具、.NET Framework 4.8 Targeting Pack、.NET SDK 9 和 PowerShell 7。`global.json` 选择兼容 VS 2022 的 .NET 9 SDK；应用仍面向 .NET Framework 4.8。

```powershell
git clone --recurse-submodules https://github.com/chocoQAQ/PCL-fix.git
cd PCL-fix
pwsh -File scripts/Build-Fix.ps1
pwsh -File scripts/Test-Download.ps1
```

构建结果位于 `artifacts/PCL-fix-<commit>.zip`。如需指定 MSBuild，可使用 `-MSBuildPath '完整的 MSBuild.exe 路径'`。

下载验证程序加载编译后 EXE 内嵌的实际依赖，检查成品中的下载限制开关，使用启动器的下载器获取 Mojang 版本列表、Minecraft 1.21.1 JSON、完整客户端 JAR、资源索引及抽样支持库和资源，并校验文件哈希。测试不需要游戏账号，文件和缓存均在 `artifacts/download-test`。它验证下载和安装，不代表已完成登录或游戏启动测试。

```powershell
# 额外执行完整原版文件安装，下载约 1 GB；不启动游戏
pwsh -File scripts/Test-Download.ps1 -FullInstall
```

## 自动同步和构建

`Sync upstream and build` 每 6 小时（UTC 00:23、06:23、12:23、18:23）检查 `Meloong-Git/PCL:main`，也支持手动运行。

1. 使用普通 Git merge 合并上游，保留修复提交，不强制覆盖分支。
2. 检查下载开关、第三方标识、设置隔离及更新地址。
3. 在 Windows runner 上构建 Release，并验证实际游戏下载。
4. 只有全部通过才推送到 `main`，随后显式触发 `Build PCL-fix`（`GITHUB_TOKEN` 的 push 本身不会触发另一次 push 工作流）。
5. 构建工作流再次验证后，将 ZIP 与 SHA-256 校验文件发布至本仓库 Releases；每个源码提交对应一个 `fix-<commit>` 标签。

冲突、上游接口变化、构建或下载检查失败时，工作流失败且不推送未经验证的合并。查看 Actions 失败日志并修复后重新运行；上游不更新时不会重复构建。上游源码的同步频率由原作者决定。

Fork 的工作流可能默认关闭：在 Actions 页面启用工作流，手动运行 `Build PCL-fix` 和 `Sync upstream and build` 各一次。需允许工作流的 `contents: write`（推送和发布）及 `actions: write`（触发构建）权限；无需额外 PAT 或仓库 Secret。主分支若设置了强制 PR 保护，则应相应调整同步流程。GitHub 可能暂停长期无活动公共仓库的定时工作流，届时需重新启用。

## 使用条款

保留并继续遵循上游 [LICENCE](LICENCE) 与其中引用的《PCL 分发有限许可》。本仓库的第三方标识、作者署名和赞助链接不代表原作者为修改版背书。Minecraft 游戏文件不随修复版分发，而由用户运行启动器时从下载源获取。
