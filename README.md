# Clippings 剪貼簿

Windows 桌面擷取與標註工具，使用 .NET 10 與 WPF 開發。程式可免安裝執行、常駐系統匣，所有資料都留在本機，不需要網路連線。此版本使用新的 APP ID `Clippings.App.New`，可與舊版 Clippings 並存。

![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![Platform](https://img.shields.io/badge/Windows-10%201809%2B-0078D4)
![License](https://img.shields.io/badge/License-MIT-yellow.svg)

## 專案說明

Clippings 解決日常截圖與複製貼上的重複工作：用全域快捷鍵快速擷取畫面、在凍結畫面上框選、在擷圖頁籤直接加上箭頭／文字／馬賽克等標註，並把擷取結果自動暫存成可編輯的頁籤。

- **離線優先**：不上傳、不連線、不收集遙測資料。
- **免安裝**：解壓縮即可執行，需安裝 .NET 10 Desktop Runtime。
- **單一實例**：重複啟動會喚出既有視窗，命令列參數會轉送給執行中的實例。
- **本機儲存**：設定與工作區暫存放在 `%LOCALAPPDATA%\Clippings.App.New`；匯出的截圖與錄影也只寫入本機資料夾。

## 功能特色

- **全域快捷鍵**：`Ctrl+Shift+X` 區域擷取、`Ctrl+Shift+F` 全螢幕、`Ctrl+Shift+W` 目前視窗、`Ctrl+Shift+D` 延遲擷取、`Ctrl+Shift+A` 螢幕標註，皆可在設定修改。
- **截圖游標**：可設定截圖是否包含滑鼠游標，預設不包含；套用於區域、全螢幕、目前螢幕、延遲與目前視窗擷取。
- **螢幕標註**：可從主畫面側欄、系統匣或全域快捷鍵啟動；直接在畫面上繪製雷射筆（漸隱軌跡）、畫筆、螢光筆、直線、箭頭、矩形、橢圓、放大鏡，附橡皮擦、清空、復原／重做、顯示／隱藏標註；浮動工具列可拖曳，支援 16 色、線寬 2–12 px、游標模式（點擊穿透）與透明／白板／黑板背景（點擊循環切換），每項工具有固定快捷鍵，標註中可一鍵擷取含標註的畫面。放大鏡跟隨游標局部放大畫面，滾輪可調整 1x～10x。工具列可自由拖曳，拖到螢幕上緣會自動吸附；圖釘可釘住工具列避免自動收合，關閉圖釘後依設定的秒數（預設 10 秒）收合成邊緣把手，滑鼠移入展開；標註期間可用滑鼠中鍵切換游標模式與工具模式。
- **目前視窗擷取**：可從主視窗、系統匣或快捷鍵擷取前景視窗；Windows 10 1903 以上優先使用 Windows.Graphics.Capture，失敗或不支援時自動改用桌面可見畫面；從 Clippings 觸發時會使用上一個外部視窗（略過系統匣、桌面等 shell 視窗）。
- **螢幕錄影**：每次可選擇要錄製的可見視窗（清單會顯示程序圖示），或直接錄製游標所在的目前螢幕；輸出 H.264 MP4，並可選擇錄製電腦播放的聲音。支援 0／5／10／20／30 秒延遲、30／60 FPS、4／8／12／20 Mbps、自訂資料夾、游標與系統音訊開關，系統音訊可調整音量（預設 +6 dB）。編碼器準備完成後才顯示倒數，倒數結束才開始錄影計時；錄影中右上角控制列可暫停、繼續或停止，也可按 `Ctrl+Shift+P` 暫停／繼續、`Ctrl+Shift+S` 停止，tooltip 會標示快捷鍵，暫停時間不計入影片長度。全螢幕錄影不顯示開始通知，避免通知出現在影片開頭。
- **凍結畫面框選**：先擷取畫面再選取，選取後可拖曳移動、拖曳邊角調整大小；`Shift` 拖曳固定正方形，方向鍵可微調；放大鏡顯示座標與 RGB、尺寸標籤、浮動操作列（確定／取消），確定後直接開啟編輯器。
- **擷圖頁籤工作區**：擷取與編輯整合在同一頁，每次擷取新增一個頁籤並自動暫存；最多 20 個頁籤，達到上限後阻止新增並提示；可逐一關閉或全部關閉，關閉後 6 秒內可從吐司復原，逾時才刪除暫存檔；每次啟動自動載入上次未儲存的頁籤；不提供收藏、查詢或重新整理。
- **整合編輯器**：平移、畫筆、螢光筆、文字、編號、直線、箭頭、矩形、橢圓、馬賽克、橡皮擦與裁切，依移動／標註／形狀／筆刷／編輯分組為分段式工具列，窄視窗時自動換行；顯示中的圖片可隨時編輯，並提供右轉／左轉／垂直翻轉／水平翻轉（註解會先合併進圖片）；裁切框選範圍後可直接複製該範圍（含註解）；圖片以 80% 置中顯示；16 色、線寬 1–20 px、復原／重做、可復原裁切，平移與縮放（`Ctrl++`／`Ctrl+-`／`Ctrl+0`）；編輯後自動寫回暫存 PNG，重啟仍可還原編輯結果；「另存圖檔」（`Ctrl+Shift+S`）匯出圖檔後自動刪除暫存檔並保留頁籤。
- **輸出設定**：自動複製、自動存檔、PNG／JPEG 格式與 JPEG 品質調整。
- **系統整合**：系統匣常駐、開啟資料夾、失敗通知、單一實例、開機啟動、九種介面主題，並強化文字、控制項邊框、卡片分隔與選取狀態的對比度；設定頁最下方提供「結束」按鈕（電源圖示）可完全關閉程式（含系統匣）。
- **命令列**：可用 `--capture-region`、`--capture-full` 等參數從腳本或捷徑觸發擷取。

## 系統需求

- Windows 10 1809（build 17763）以上，x64 或 ARM64。
- 需要 .NET 10 Desktop Runtime（x64 或 ARM64）。
- 從原始碼建置需要 .NET SDK 10.0。

## 快速開始

### 下載

前往 [GitHub Releases](https://github.com/mark216tw/win-clippings-02/releases/latest) 選擇適合的版本，解壓縮後執行 `Clippings.App.New.exe`。

| 平台 | 檔案 | 大小 | 需求 |
| --- | --- | ---: | --- |
| x64 | `Clippings-1.34.2-win-x64-framework-dependent.zip` | 約 14.2 MB | 需安裝 .NET 10 Desktop Runtime（x64） |
| ARM64 | `Clippings-1.34.2-win-arm64-framework-dependent.zip` | 約 13.6 MB | 需安裝 .NET 10 Desktop Runtime（ARM64） |

僅提供 framework-dependent ZIP，不提供安裝檔。.NET 10 Desktop Runtime 下載：<https://dotnet.microsoft.com/download/dotnet/10.0>

### 從原始碼建置

```powershell
dotnet build
dotnet test
```

執行 Debug 版：

```powershell
src\Clippings.App\bin\x64\Debug\net10.0-windows10.0.19041.0\Clippings.App.New.exe
```

製作 x64 精簡版（需目標電腦已安裝 .NET 10 Desktop Runtime）：

```powershell
dotnet publish src/Clippings.App/Clippings.App.csproj -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=false
```

製作 ARM64 精簡版：

```powershell
dotnet publish src/Clippings.App/Clippings.App.csproj -c Release -r win-arm64 --self-contained false -p:PublishReadyToRun=false
```

## 使用方式

| 快捷鍵 | 功能 |
| --- | --- |
| `Ctrl+Shift+X` | 區域擷取（框選） |
| `Ctrl+Shift+F` | 全螢幕擷取 |
| `Ctrl+Shift+W` | 目前視窗擷取 |
| `Ctrl+Shift+D` | 延遲擷取（預設 5 秒） |
| `Ctrl+Shift+A` | 開關螢幕標註模式 |

- 從系統匣圖示按左鍵開啟主視窗、按右鍵開啟選單。
- 從主視窗錄影卡片選擇「選擇視窗錄影」或「全螢幕錄影」，也可從系統匣使用相同入口；完成後選擇「停止錄影」。預設輸出位於 `影片\Clippings.App.New`，可在設定修改延遲與其他錄影選項。
- 主視窗分成「擷圖」與「設定」兩頁；「擷圖」頁以頁籤管理擷取結果並直接編輯。
- 在擷圖頁籤按右鍵選擇「重新命名」，輸入名稱後按 Enter 確認、Esc 取消；自訂名稱僅保留於本次執行期間。
- 框選完成後按 `Enter` 或 `Ctrl+E` 確定並建立擷圖頁籤；按「複製」或 `Ctrl+C` 只將框選範圍複製到剪貼簿，不建立頁籤也不自動存檔；按「取消」或 `Esc` 會清除選取並回到框選前狀態，無選取時再按 `Esc` 才離開擷取。全域快捷鍵擷取取消後不會自動開啟主視窗；主視窗、系統匣與全域快捷鍵擷取完成後會顯示主視窗並選取新頁籤。
- 擷圖頁籤中的編輯器支援 `Ctrl+Z` 復原、`Ctrl+Y` 重做、`Ctrl+C` 複製、`Ctrl+S`／`Ctrl+Shift+S` 另存新檔、`Ctrl++`／`Ctrl+-` 縮放、`Ctrl+0` 符合視窗。
- 擷圖頁面標題列右側的「1:1」可切換至原始像素檢視（圖片 1 像素對應螢幕 1 像素），同一列也會顯示圖片尺寸與縮放比例；顯示時仍預設 80%。縮放比例的提示會列出邏輯與實際像素比例。
- 螢幕標註模式中：拖曳繪製、右鍵取消目前筆畫、`Ctrl+Z`／`Ctrl+Y` 復原重做、`Esc` 或工具列 X 退出；「游標模式」讓點擊穿透到底下的應用程式。工具列可自由拖曳，拖到螢幕上緣會吸附；圖釘可釘住工具列避免自動收合，關閉圖釘後依設定的秒數收合成邊緣把手，滑鼠移入把手展開；中鍵可在游標模式與工具模式間切換（可在設定頁關閉）。
- 螢幕標註工具快捷鍵（數字列或字母）：`1/L` 雷射筆、`2/P` 畫筆、`3/H` 螢光筆、`4/N` 直線、`5/A` 箭頭、`6/R` 矩形、`7/O` 橢圓、`8/E` 橡皮擦、`9/V` 顯示/隱藏標註、`0/C` 游標模式。

完整操作說明請見 [使用指南](docs/USER_GUIDE.md)。

## 命令列參數

| 參數 | 說明 |
| --- | --- |
| `--show` | 顯示主視窗（與直接再次啟動相同） |
| `--capture-region` | 區域擷取 |
| `--capture-full` | 全螢幕擷取 |
| `--capture-monitor` | 目前螢幕擷取（游標所在螢幕） |
| `--capture-window` | 目前視窗擷取（觸發當下的前景視窗） |
| `--annotate` | 開關螢幕標註模式 |
| `--autostart` | 開機啟動時不顯示主視窗 |
| `--help` | 顯示參數說明 |

- 指令會轉送給已執行的 Clippings（單一實例）；尚未執行時會先啟動並常駐系統匣。
- 參數不分大小寫，一次只能指定一個指令。
- CLI 觸發的擷取完成後會建立擷圖頁籤，但不會顯示主視窗，也不播放提示音；複製與自動存檔仍依設定（區域框選沿用框選流程，不自動存檔）。
- 指令送出後即結束，退出碼 `0` 表示成功、`1` 表示轉送失敗、`2` 表示參數錯誤；`0` 不代表擷取已完成。
- 從 PowerShell 取得輸出與退出碼：

```powershell
$p = Start-Process .\Clippings.App.New.exe -ArgumentList '--help' -Wait -NoNewWindow -PassThru
$p.ExitCode
```

- `--capture-window` 擷取觸發當下的前景視窗，從終端機呼叫通常會擷取到終端機視窗；區域框選仍需手動操作。

## 資料位置

| 用途 | 路徑 |
| --- | --- |
| 擷圖頁籤暫存 | `%LOCALAPPDATA%\Clippings.App.New\workspace\` |
| 設定 | `%LOCALAPPDATA%\Clippings.App.New\settings.json` |
| 日誌 | `%LOCALAPPDATA%\Clippings.App.New\logs\` |
| 預設自動存檔資料夾 | `圖片\Clippings.App.New`（可在設定變更） |
| 螢幕錄影 | `影片\Clippings.App.New`（可在設定修改） |

程式不會將資料傳送到任何伺服器；詳細說明請見 [隱私說明](docs/PRIVACY.md)。

## 專案結構

```
src/Clippings.App/        WPF：主視窗、擷圖頁籤工作區、內嵌編輯器、擷取覆蓋層、系統匣、熱鍵
src/Clippings.Platform/   Win32 擷取、螢幕與 DPI、SkiaSharp 影像、設定、單一實例
src/Clippings.Core/       模型、註釋、PixelRect、UndoStack、設定與雜湊（可單元測試）
tests/Clippings.Core.Tests/
```

- UI：WPF（本專案依使用者決定不採 WinUI 3，原因見 [開發紀錄](docs/DEV_LOG.md)）。
- 影像：SkiaSharp（Win2D 不支援 WPF 的控制項裝載）。
- 資料：設定以 JSON 儲存；擷圖頁籤以 `workspace\` 內的 PNG 暫存檔管理。
- 套件版本集中於 `Directory.Packages.props`，版本與組件資訊集中於 `Directory.Build.props`。

## 文件

| 文件 | 內容 |
| --- | --- |
| [文件索引](docs/README.md) | 所有文件的導覽 |
| [使用指南](docs/USER_GUIDE.md) | 安裝、擷取、擷圖頁籤、編輯器、設定與疑難排解 |
| [系統架構](docs/ARCHITECTURE.md) | 分層架構、執行時元件、主要流程與執行緒模型 |
| [技術文件](docs/TECHNICAL.md) | 技術棧、關鍵類別、擷取／影像／儲存／通訊細節 |
| [系統設計](docs/SYSTEM_DESIGN.md) | 需求、設計決策、取捨、失敗處理與後續規劃 |
| [建置與發佈](docs/BUILD_AND_RELEASE.md) | 開發指令、發佈流程與版本規範 |
| [隱私說明](docs/PRIVACY.md) | 本機資料與日誌政策 |
| [開發紀錄](docs/DEV_LOG.md) | 各版本開發過程、問題與驗證 |
| [變更紀錄](CHANGELOG.md) | 版本功能異動 |
| [版本說明](RELEASE_NOTES.md) | 給使用者的版本摘要 |

## 開發

```powershell
dotnet build
dotnet test
dotnet format --verify-no-changes
```

- 開發與測試一律使用 `Debug`；`Release` 僅在發佈時建立。
- 使用者可見文字集中在 `src/Clippings.App/Strings/AppStrings.cs`，待後續改為 `.resx` 多語系。
- 貢獻方式請見 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 已知限制

- Windows 10 1809 或 Windows.Graphics.Capture 不可用時，目前視窗擷取會退回桌面可見畫面；最小化、移出螢幕或受保護的內容仍可能無法完整擷取。
- 滾動長截圖、跨螢幕框選尚未實作。
- 錄影需要 Windows 10 1903 以上；支援單一視窗或目前螢幕，以及可選的系統音訊，但不支援區域錄影、麥克風或影片編輯。目標視窗改變尺寸或最小化時會自動停止；DRM、獨占模式或特殊音效驅動可能無法錄到聲音。
- 螢幕標註的筆畫不能跨越螢幕邊界（每個螢幕獨立畫布）；雷射筆在游標模式下不會顯示軌跡。放大鏡需要 Windows 10 2004（build 19041）以上。
- 混合 DPI 多螢幕環境僅以 100% 縮放實測。
- 另存新檔成功後會刪除暫存檔並保留頁籤；該頁籤在本次工作階段仍可切換與編輯，但重新啟動後不會還原。
- 新 APP ID 使用全新的 `%LOCALAPPDATA%\Clippings.App.New`，不會自動匯入舊版 `Clippings` 的資料。
- 尚未提供多語系資源檔，介面文字集中於 `src/Clippings.App/Strings/AppStrings.cs`。
- 受保護內容（DRM、全螢幕獨佔遊戲）可能無法擷取。

若 Windows 工作列右鍵選單仍顯示舊圖示，請先解除舊的固定項目，再從新版 `Clippings.App.New.exe` 重新固定；該圖示由 Windows Shell 捷徑與圖示快取管理。免安裝版無法自動改寫捷徑上的 AppUserModelID，若需要完整的工作列釘選識別，仍需由安裝程式設定捷徑屬性。

## 授權

本專案採用 [MIT License](LICENSE)。
