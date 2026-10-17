# Power Option Editor

查看并修改 Windows 电源方案里的全部设置，包括系统高级电源选项中隐藏的项目。64 位，单个可执行文件，界面随系统语言使用简体中文、繁体中文或英文。

许可：[GNU GPL v3](LICENSE)。

## 运行

需要 64 位 Windows，并已安装 .NET Framework 4。Windows Server 2003 SP2 x64 可以使用旧式电源方案；Windows Vista 及更新的系统使用完整的电源设置。

```powershell
.\build.ps1
.\PowerOptionEditor.exe
```

写入电源设置如果被拒绝，使用程序里的「以管理员重启」。

## 用法

- 选择电源方案，查看分组后的设置，以及交流电、电池和该方案的默认值。
- 在下方修改数值后点「写入数值」。勾选「在系统电源选项中隐藏」会同时写入隐藏属性。
- 双击一行，或点「打开注册表」，打开该项在当前方案下的注册表位置。
- 「导出」和「导入」使用与 Power Settings Explorer 相同的 XML：`root / subgroup / setting / acindex / dcindex`。

## 构建

`build.ps1` 调用本机的 `csc.exe`，目标平台是 x64，清单在 `app.manifest`。不需要 Visual Studio。如果用 Visual Studio 打开，请保持 .NET Framework 4.x 和 x64，不要改成 .NET 6 或更新的版本。
