# Power Option Editor

查看并修改 Windows 电源方案设置，包括新版 Windows 隐藏的高级选项。界面支持简体中文、繁体中文和英文。许可：[GNU GPL v3](LICENSE)。

## 工程与开源编辑器

这是标准的传统 C# / MSBuild 4.0 工程，目标固定为 **.NET Framework 4.0 完整版、x64**，没有第三方运行依赖。

- SharpDevelop：打开 `PowerOptionEditor.sln`，选择 `Debug | x64` 或 `Release | x64` 后构建。此格式供支持传统 .NET Framework 工程的 IDE 使用。
- VSCodium：打开项目目录，执行默认构建任务（Ctrl+Shift+B）。代码补全需要自行配置支持传统 C# 工程的扩展，构建不依赖扩展。
- 命令行：运行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`。

构建机器需要 Windows、.NET Framework 的 MSBuild，以及 **4.0 引用程序集**（例如 Windows SDK 7.1 提供的目标程序集）。仅安装新版 .NET SDK 不等于安装了这些组件。现代构建机器也可以通过 NuGet 包 `Microsoft.NETFramework.ReferenceAssemblies.net40` 取得引用程序集，将包中 `build\.NETFramework\v4.0` 的绝对路径传给脚本：

```powershell
.\build.ps1 -Configuration Release -FrameworkPath 'D:\reference-assemblies\v4.0'
.\build.ps1 -Configuration Debug -FrameworkPath 'D:\reference-assemblies\v4.0'
```

也可直接构建：

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe PowerOptionEditor.sln /t:Rebuild /p:Configuration=Release /p:Platform=x64
```

输出位于 `bin\Release\` 或 `bin\Debug\`。部署时只需复制 `PowerOptionEditor.exe`；PDB 仅用于调试，不需要额外的 `.exe.config` 文件。目标电脑仍需安装 .NET Framework 4.0 完整版或兼容的更新版本。构建脚本使用自身目录定位工程，可以从其他工作目录调用。

## 最低运行环境

**Windows Server 2003 SP2 x64，安装 .NET Framework 4.0 完整版（包含 x64 CLR）**。不包括未安装 SP2 的 Server 2003。微软的 [.NET Framework 4 系统要求](https://www.microsoft.com/en-us/download/details.aspx?id=17851)列出了 Server 2003 SP2。

请保持目标版本为 4.0；不要升级为 4.5 或更新版，也不要改成现代 .NET。工程对框架版本与 x64 目标做了构建检查。当前工程转换保留原有程序行为：

- Server 2003 使用 `EnumPwrSchemes / ReadPwrScheme / WritePwrScheme` 等旧式电源接口。
- Vista 及更新系统探测并使用新版电源接口；Server 2003 没有新版隐藏属性和 XML 电源方案导入、导出功能。
- 新版 DPI API 缺失时已有回退处理。

编译成功不能替代真实旧系统验证。发布前应在 Server 2003 SP2 x64 虚拟机安装 .NET 4.0，检查启动、枚举电源方案、读取和写入旧式设置；先记录原值，验证后恢复。本工程改造未在真实 Server 2003 上执行运行验收。

## 用法

选择电源方案，查看分组设置，修改交流电、电池数值后点「写入数值」。新版系统可以修改隐藏属性，双击项目打开注册表，并导入、导出兼容 Power Settings Explorer 的 XML。写入被拒绝时可以使用「以管理员重启」。

只读诊断（不修改电源设置）：

```powershell
.\bin\Release\PowerOptionEditor.exe /dump D:\power-settings-dump.txt
```
