# encrypt4dwg 构建与打包指南

本项目面向 Windows 10/11 发布，最终交付物为安装程序 `encrypt4dwg-Setup.exe`，而不是 .NET 的 `publish` 构建目录或 ZIP 文件。

## 前置条件

在用于构建的 Windows 电脑上安装：

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)；
- [Inno Setup 6](https://jrsoftware.org/isinfo.php)。

使用 PowerShell 进入 Windows 客户端项目目录：

```powershell
cd D:\path\to\encrypt4dwg
dotnet --info
```

`dotnet --info` 应显示 .NET 8 SDK。首次构建或还原依赖时需要联网。

## 构建 Windows 程序

先还原依赖并执行编译：

```powershell
dotnet restore -r win-x64
dotnet build -c Release --no-restore
```

再发布 64 位、自包含版本：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true --no-restore
```

发布目录为：

```text
bin\Release\net8.0-windows\win-x64\publish\
```

主程序是 `encrypt4dwg.exe`。虽然启用了单文件发布，WPF 仍可能包含若干原生 DLL；测试或运行时必须保留发布目录内的全部文件。

## 生成安装程序

安装脚本位于 `Installer\ecrypt4Dwg.iss`。使用 Inno Setup 编译：

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\ecrypt4Dwg.iss
```

### 首次使用 Inno Setup 6：图形界面方式

1. 访问 [Inno Setup 官网](https://jrsoftware.org/isinfo.php)，下载并安装 Inno Setup 6。安装过程保持默认选项即可；建议安装 64 位版本。
2. 确认上面的 `dotnet publish` 已经成功执行，且下列文件存在：

   ```text
   bin\Release\net8.0-windows\win-x64\publish\encrypt4dwg.exe
   ```

3. 在开始菜单搜索并打开 **Inno Setup Compiler**。
4. 在菜单中选择 **File → Open**，打开项目中的 `Installer\ecrypt4Dwg.iss`。
5. 检查窗口下方的输出区域没有脚本错误后，选择 **Build → Compile**，或直接按 `Ctrl+F9`。
6. 等待状态显示编译完成；选择 **Build → Open Output Folder**，或在资源管理器打开 `dist` 目录。
7. 生成的 `encrypt4dwg-Setup.exe` 就是应交付给最终用户的安装程序。双击它会进入安装向导，默认安装至 `C:\Program Files\encrypt4dwg`，用户可自行更改目录。

### 命令行方式

在仓库根目录打开 PowerShell，执行：

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\ecrypt4Dwg.iss
```

若该路径不存在，在开始菜单中右键 **Inno Setup Compiler**，选择“打开文件所在的位置”确认实际安装目录，再替换命令中的路径。Inno Setup 的 `ISCC.exe` 支持以脚本文件作为命令行参数编译；成功时退出代码为 `0`。[官方命令行说明](https://jrsoftware.org/ishelp/topic_compilercmdline.htm)

生成结果：

```text
dist\encrypt4dwg-Setup.exe
```

安装器默认安装到 `C:\Program Files\encrypt4dwg`，并允许用户在安装向导中选择其他目录。它会创建开始菜单快捷方式，并可选创建桌面快捷方式；卸载时会删除 `ecrypt4DwgCheck` 计划任务。

## GitHub 自动构建与发布

仓库中的 `.github/workflows/build-release.yml` 会在 GitHub 的 Windows Runner 上自动执行以下流程：还原并编译 .NET 8 项目、发布 `win-x64` 自包含程序、使用 Inno Setup 生成安装包，并上传安装包作为 Actions artifact。每次推送 `main` 或向 `main` 提交 Pull Request 都会执行构建验证。

当推送符合 `vX.Y.Z` 格式的版本标签时，例如 `v1.2.0`，工作流还会自动创建同名 GitHub Release，将安装包作为 Release 附件上传，并把安装包内部版本设为 `1.2.0`。

完成一次正式发布：

```powershell
git switch main
git pull --ff-only origin main
git tag v1.2.0
git push origin v1.2.0
```

随后在 GitHub 仓库的 **Actions** 页面查看构建日志；成功后，在 **Releases** 页面下载 `encrypt4dwg-Setup.exe`。标签必须以 `v` 开头且版本为三段数字，否则工作流会拒绝发布，避免产生含糊版本号。

### 常见问题

- **提示找不到 `publish` 中的文件**：先在仓库根目录执行完整的 `dotnet publish` 命令，再重新编译 `.iss` 脚本。安装脚本中的源路径是相对 `Installer` 目录计算的，不能单独复制 `.iss` 文件到其他位置编译。
- **安装时要求管理员权限**：这是预期行为，因为默认目录为 `Program Files`。以管理员身份运行安装程序，或在安装向导中改用当前用户有写权限的目录。
- **Windows SmartScreen 警告**：未签名的新安装包可能触发提示。正式对外交付前应使用企业代码签名证书为 `encrypt4dwg-Setup.exe` 签名。
- **只复制 `encrypt4dwg.exe` 后无法运行**：不要单独复制 EXE；应交付安装器，或在测试时复制整个 `publish` 目录。

## 验证与交付

在干净的 Windows 10/11 虚拟机或测试电脑上执行：

1. 运行 `dist\encrypt4dwg-Setup.exe` 并完成安装。
2. 启动 `encrypt4dwg`，选择一个无敏感内容的测试文件；默认时间为 30 天后的 `00:00:00`，也可手动填写精确到秒的锁定时间。
3. 确认私钥保存到项目目录以外的位置，并确认 Windows 任务计划程序存在 `ecrypt4DwgCheck`。
4. 到期后确认原文件被删除、同目录生成 `.locked` 文件。
5. 使用保存的私钥执行界面中的“使用私钥解锁”，确认恢复内容与原文件一致。

对外发布前建议为 `dist\encrypt4dwg-Setup.exe` 进行 Windows 代码签名，以减少 SmartScreen 警告；不要将测试私钥、DWG 文件或本地注册表纳入安装包。
