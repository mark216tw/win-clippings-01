# 建置與發佈

本文件說明開發環境、建置、測試、封裝與 GitHub Release 流程。

---

## 1. 開發環境

| 項目 | 需求 |
| --- | --- |
| 作業系統 | Windows 10 1809（build 17763）以上，x64 或 ARM64 |
| .NET SDK | 10.0 |
| IDE | Visual Studio 2022／2026、JetBrains Rider 或 VS Code（可選） |

確認 SDK：

```powershell
dotnet --info
```

---

## 2. 還原與建置

```powershell
dotnet restore Clippings.sln
dotnet build Clippings.sln
```

- 開發與測試一律使用 `Debug`。
- 啟用 `TreatWarningsAsErrors`，警告視為錯誤。
- 輸出位於 `src/*/bin/x64/Debug/net10.0-windows10.0.19041.0/`。

執行 Debug 版：

```powershell
src\Clippings.App\bin\x64\Debug\net10.0-windows10.0.19041.0\Clippings.exe
```

---

## 3. 測試與格式檢查

```powershell
dotnet test Clippings.sln
dotnet format Clippings.sln --verify-no-changes --no-restore
```

測試專案：`tests/Clippings.Core.Tests`，涵蓋快捷鍵、幾何、復原堆疊、設定、雜湊與命令列解析。

---

## 4. 發佈版本

本專案在 GitHub Release 僅提供 framework-dependent（非 self-contained）版本，分為 x64 與 ARM64：

| 平台 | 特性 | 需求 | 約略大小 |
| --- | --- | --- | ---: |
| x64 | framework-dependent，關閉 ReadyToRun | .NET 10 Desktop Runtime（x64） | 18 MB |
| ARM64 | framework-dependent，關閉 ReadyToRun | .NET 10 Desktop Runtime（ARM64） | 18 MB |

僅在明確需要發佈時執行 Release。

### 4.1 x64 精簡版（framework-dependent）

```powershell
dotnet publish src/Clippings.App/Clippings.App.csproj `
  -c Release -r win-x64 --self-contained false `
  -p:PublishReadyToRun=false `
  -o artifacts\publish-win-x64-fx
```

- 不包含 .NET 與 WPF 執行階段，因此檔案明顯較小。
- 目標電腦需安裝 **.NET 10 Desktop Runtime（x64）**。
- `PublishReadyToRun=false`：避免預先編譯增加體積。
- 仍會在應用程式目錄包含 SkiaSharp、SQLite 等原生函式庫。

### 4.2 ARM64 精簡版（framework-dependent）

```powershell
dotnet publish src/Clippings.App/Clippings.App.csproj `
  -c Release -r win-arm64 --self-contained false `
  -p:PublishReadyToRun=false -p:Platform=arm64 -p:PlatformTarget=arm64 `
  -o artifacts\publish-win-arm64-fx
```

- 目標電腦需安裝 **.NET 10 Desktop Runtime（ARM64）**。
- 其餘設定與 x64 精簡版相同。

### 4.3 打包 ZIP

```powershell
$version = "1.19.0"
New-Item -ItemType Directory -Force artifacts | Out-Null

# x64 精簡版（排除 PDB）
robocopy artifacts\publish-win-x64-fx artifacts\staging-fx /E /XF *.pdb /NFL /NDL /NJH /NJS /NP
Compress-Archive -Path "artifacts\staging-fx\*" `
  -DestinationPath "artifacts\Clippings-$version-win-x64-framework-dependent.zip" -Force

# ARM64 精簡版（排除 PDB）
robocopy artifacts\publish-win-arm64-fx artifacts\staging-arm64-fx /E /XF *.pdb /NFL /NDL /NJH /NJS /NP
Compress-Archive -Path "artifacts\staging-arm64-fx\*" `
  -DestinationPath "artifacts\Clippings-$version-win-arm64-framework-dependent.zip" -Force

Get-FileHash "artifacts\Clippings-$version-win-x64-framework-dependent.zip" -Algorithm SHA256
Get-FileHash "artifacts\Clippings-$version-win-arm64-framework-dependent.zip" -Algorithm SHA256
```

`artifacts/` 與 `publish/` 已在 `.gitignore` 中排除，不會提交到版本控制。PDB 檔（尤其 `libSkiaSharp.pdb`）體積很大，發佈時應排除。

---

## 5. 版本規範

版本集中於 `Directory.Build.props`：

```xml
<Version>1.19.0</Version>
<AssemblyVersion>1.19.0.0</AssemblyVersion>
<FileVersion>1.19.0.0</FileVersion>
```

- `Version`：`主.次.修`，每次功能變更遞增。
- `AssemblyVersion`：只在主次版變更時調整，避免破壞組件參考。
- `FileVersion`：跟隨 `Version`。
- 版本變更需同步更新 `CHANGELOG.md`、`RELEASE_NOTES.md` 與 `docs/DEV_LOG.md`。

---

## 6. 發佈檢查清單

1. 確認 `Directory.Build.props` 版本正確。
2. 更新 `CHANGELOG.md`。
3. 更新 `RELEASE_NOTES.md`（給使用者的摘要）。
4. 更新 `docs/DEV_LOG.md`。
5. 執行 `dotnet build`、`dotnet test`、`dotnet format --verify-no-changes`。
6. 建立 x64 與 ARM64 精簡版發佈，並各自打包 ZIP（排除 PDB）。
7. 在已安裝 .NET 10 Desktop Runtime 的 x64 與 ARM64 環境測試精簡版。
8. 建立 Git commit 與 tag。
9. 建立 GitHub Release 並上傳兩個 ZIP。
10. 確認 GitHub 正確辨識 MIT License。

---

## 7. GitHub Release 流程

```powershell
# 建立標籤
git tag v1.19.0
git push origin v1.19.0

# 建立 Release 並上傳兩個資產
gh release create v1.19.0 `
  "artifacts\Clippings-1.19.0-win-x64-framework-dependent.zip" `
  "artifacts\Clippings-1.19.0-win-arm64-framework-dependent.zip" `
  --title "Clippings 剪貼簿 1.19.0" `
  --notes-file "artifacts\release-notes-1.19.0.md"
```

若 Release 已存在，改用 `gh release upload` 補上資產：

```powershell
gh release upload v1.19.0 "artifacts\Clippings-1.19.0-win-x64-framework-dependent.zip" "artifacts\Clippings-1.19.0-win-arm64-framework-dependent.zip"
gh release edit v1.19.0 --notes-file "artifacts\release-notes-1.19.0.md"
```

`release-notes` 檔案可從 `RELEASE_NOTES.md` 的最新區段擷取，或使用 `--notes` 直接輸入。

---

## 8. 授權與第三方元件

- 本專案採用 MIT License，全文見根目錄 `LICENSE`。
- 主要第三方套件：CommunityToolkit.Mvvm、H.NotifyIcon.Wpf、SkiaSharp、EF Core、Microsoft.Extensions.Hosting。
- 各套件依其原始授權條款使用；發佈時請保留相關授權資訊。

---

## 9. 常見問題

### 建置失敗：找不到 .NET 10

安裝 .NET SDK 10.0，並確認 `dotnet --list-sdks` 包含 10.x。

### NuGet 還原失敗

確認網路與 NuGet 來源設定，或先執行 `dotnet restore Clippings.sln`。

### 發佈後缺少檔案

確認將整個 `publish` 目錄內容打包，而不是只複製 `Clippings.exe`。精簡版仍需要同目錄的 `Clippings.dll`、`Clippings.runtimeconfig.json` 與原生函式庫。

### 精簡版無法啟動

確認目標電腦已安裝對應平台的 .NET 10 Desktop Runtime（x64 或 ARM64），而不是只有 .NET Runtime 或舊版 .NET。

### Release 上傳失敗

確認 `gh auth status` 已登入正確帳號，且具備 `repo` 權限。
