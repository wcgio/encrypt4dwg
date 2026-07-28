# ecrypt4Dwg

`ecrypt4Dwg` 是面向 Windows 10/11 的 DWG 定时加密桌面工具，使用 C#、WPF 和 .NET 8 实现。

## 功能

- 选择 DWG 或任意文件，默认锁定时间为 30 天后的 `00:00:00`，也可精确设置到秒；
- 到期后使用 RSA-3072 + AES-256-GCM 生成 `.locked` 文件，并在密文写入成功后删除明文；
- 使用 Windows Task Scheduler 每分钟执行检查；网络可用时优先使用 HTTPS 服务器时间，网络不可用时回退至本机时间；
- 支持取消未执行任务，以及使用创建时保存的 PEM 私钥解锁；
- 任务注册表使用当前 Windows 用户的 DPAPI 保护。

## 构建与安装

完整构建、发布、Inno Setup 打包和验证步骤见 [BUILD.md](BUILD.md)。正式交付物是 `dist\ecrypt4Dwg-Setup.exe`。

## 安全边界

这是本地定时加密与流程约束工具，不是 DRM。它不能阻止甲方在到期前复制明文、禁用计划任务、修改本地程序或绕过本机时间。私钥是解锁的唯一凭证；请在创建任务后立即将私钥转移到项目目录以外的安全位置。

应用启动异常会记录到 `%LOCALAPPDATA%\ecrypt4Dwg\startup-error.log`。
