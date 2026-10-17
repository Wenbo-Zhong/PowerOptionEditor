$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    Write-Error ".NET Framework 4 compiler was not found: $csc"
    exit 1
}
& $csc /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /win32manifest:app.manifest /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll /out:PowerOptionEditor.exe Program.cs
exit $LASTEXITCODE
