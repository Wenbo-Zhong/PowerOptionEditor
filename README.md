# Power Option Editor

查看并修改 Windows 电源方案设置，包括新版 Windows 隐藏的高级选项。界面支持简体中文、繁体中文和英文。许可：[GNU GPL v3](LICENSE)。

## 参与翻译

界面文案集中在 [`locales/`](locales/)：`en.xml`、`zh-Hans.xml` 和 `zh-Hant.xml`。新增语言只需复制英文文件、翻译文本并提交对应语言的 XML，无需修改 C# 或工程文件。程序按系统界面语言选择翻译，缺失条目回退英文；语言文件自动嵌入 EXE。贡献步骤见 [Translation guide (English)](locales/README.md)，校验命令为 `python scripts/validate-locales.py`。

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

日常页的「取消隐藏所有隐藏选项」会处理完整设置列表，不受搜索条件限制，仅清除隐藏标志，保留其它属性和电源数值，并报告成功与失败数量。场景菜单仅显示至少存在一个交流电或电池覆盖值的场景；空键不算可调整目标。

「场景」页支持处理器电源管理的低延迟、持续性能、游戏、低功耗、待机、关屏、受限、高耗电、实用程序、效率模式、多媒体、入门性能和后台场景。场景按本机 `Profile\Events` 的优先级降序排列，并隐藏没有可调整覆盖值的场景。日常、场景选项卡包含各自的工具栏、设置列表和编辑区。场景页仅显示场景菜单，自动汇总该场景实际存在的方案及滑块覆盖层；多个来源在列表分组中标注，保存仍写回原方案。

场景值直接读取 `PowerSettings\<处理器子组>\<设置>\<场景>\<方案>` 下的 `ProvAcSettingIndex` / `ProvDcSettingIndex`，不使用日常值代替。交流电和电池均无覆盖值的设置不显示；仅一侧缺失时，该侧显示「未覆盖」且不可编辑。查看和保存均不会创建缺失的键或字段。频率上限为 0 时显示「不封顶」。场景保存只写现有的这两个 DWORD 字段，不切换或重新激活电源方案，下次进入该场景时生效。场景页不提供日常方案的默认值恢复、隐藏属性修改及 XML 导入导出；旧式系统禁用场景页。

只读诊断（不修改电源设置）：

```powershell
.\bin\Release\PowerOptionEditor.exe /dump D:\power-settings-dump.txt
```
