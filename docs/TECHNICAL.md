# 技術文件

本文件提供 Clippings 的技術棧、關鍵類別、平台實作、資料格式與開發慣例，主要給維護者與貢獻者參考。高階說明請見 [系統架構](ARCHITECTURE.md)。

---

## 1. 技術棧

| 項目 | 版本／設定 |
| --- | --- |
| 語言 | C#（nullable、file-scoped namespace、ImplicitUsings） |
| 執行環境 | .NET 10 |
| 目標框架 | `net10.0-windows10.0.19041.0` |
| 最低系統 | Windows 10 1809（build 17763） |
| UI | WPF（`UseWPF`） |
| MVVM | `CommunityToolkit.Mvvm` |
| 系統匣 | `H.NotifyIcon.Wpf` |
| 影像 | `SkiaSharp`、`SkiaSharp.Views.WPF` |
| 資料 | EF Core 10 + SQLite |
| 日誌 | `Microsoft.Extensions.Logging` + 自訂檔案 Provider |
| 測試 | xUnit、Microsoft.NET.Test.Sdk |
| 平台 | x64 |

套件版本集中於 `Directory.Packages.props`，編譯設定與版本集中於 `Directory.Build.props`。

```xml
<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
<TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<Platforms>x64</Platforms>
```

---

## 2. 方案結構

```
Clippings.sln
Directory.Build.props          共用編譯設定與版本
Directory.Packages.props       集中套件版本
src/
  Clippings.Core/
    Editing/                   PixelRect、ResizeEdges、UndoStack
    Helpers/                   HashUtil、PathUtil
    Input/                     HotkeyGesture
    Models/                    AppSettings、Annotation、CaptureItem、ClipboardItem…
    Services/                  IClippingsStore、ISettingsStore
    Startup/                   CommandLineOptions、StartupCommand
  Clippings.Platform/
    Capture/                   ScreenCaptureService
    Display/                   MonitorInfo、MonitorService
    Imaging/                   ImageCodec
    Interop/                   NativeMethods、WindowHelper、ForegroundWindowTracker、ConsoleBridge
    SingleInstance/            SingleInstanceService、ActivationRequestedEventArgs
    Storage/                   ClippingsDbContext、ClippingsStore、JsonSettingsStore、StartupRegistrar
  Clippings.App/
    Views/                     MainWindow、HistoryPage、ClipboardPage、SettingsPage
                               CaptureOverlayWindow、EditorWindow
    Services/                  AppState、CaptureCoordinator、ClipboardWatcher、GlobalHotkeyService
                               ThemeService、TrayNotificationService
    Helpers/                   BitmapSourceHelper、ClipboardHelper、PathToImageConverter…
    Logging/                   FileLoggerProvider
    Strings/                   AppStrings
    Styles/                    共用樣式與九種主題資源字典
tests/
  Clippings.Core.Tests/
```

---

## 3. Core 層

### 3.1 模型

| 型別 | 用途 |
| --- | --- |
| `CaptureItem` | 擷取紀錄：Id、時間、尺寸、原圖路徑、縮圖路徑、SHA-256、收藏、來源 |
| `ClipboardItem` | 剪貼簿紀錄：Id、時間、`ClipboardItemKind`、文字、圖檔路徑、縮圖路徑、雜湊、釘選 |
| `ClipboardItemKind` | `Text`、`Image`、`Files`（現行僅記錄 `Image`，其餘值保留相容既有資料） |
| `CaptureMode` | `Region`、`FullScreen`、`Monitor`、`Delayed`、`Window` |
| `ImageFormat` | `Png`、`Jpeg` |
| `AppSettings` | 全部使用者設定；`Clone()` 提供獨立副本 |
| `AppTheme` | `System`、`Light`、`Dark`、`WarmPaper`、`SoftSage`、`DuskBlue`、`DuskPurple`、`InkGreenNight`、`MistBlueMorning` |
| `Annotation` | 標註資料：工具、點集合、ARGB 顏色、線寬、字級、文字、編號 |
| `AnnotationTool` | `Arrow`、`Line`、`Rectangle`、`Ellipse`、`Pen`、`Highlighter`、`Text`、`Mosaic`、`Number`、`Crop`、`Pan` |
| `PixelRect` | 整數像素矩形，提供交集、裁切、移動、縮放邊界運算 |
| `UndoStack<T>` | 固定容量復原堆疊，支援丟棄回呼與重做 |
| `HotkeyGesture` | 快捷鍵解析、顯示名稱與虛擬鍵對應 |
| `CommandLineOptions` | 命令列參數解析 |
| `PathUtil` | 資料夾、檔名與路徑規則 |
| `HashUtil` | SHA-256 十六進位字串 |

### 3.2 儲存介面

- `IClippingsStore`：擷取與剪貼簿的 CRUD、擷取圖片覆寫（`UpdateCaptureImageAsync`）、批次刪除未收藏擷取（`DeleteNonFavoriteCapturesAsync`）、收藏／釘選、計數、保留策略。
- `ISettingsStore`：設定載入與儲存。

介面放在 `Core`，實作放在 `Platform`，讓 UI 與商業流程不直接相依 EF Core。

---

## 4. Platform 層

### 4.1 螢幕擷取

`ScreenCaptureService` 使用 GDI：

1. `GetDC(IntPtr.Zero)` 取得螢幕裝置內容。
2. `CreateCompatibleDC` 建立記憶體 DC。
3. `CreateDIBSection` 建立 32-bit 點陣圖區段。
4. `BitBlt` 以 `SRCCOPY | CAPTUREBLT` 複製指定矩形。
5. 使用 `SKBitmap.InstallPixels` 直接包裝像素記憶體，並在釋放時呼叫 `DeleteObject`。
6. 將 Alpha 通道強制設為 `0xFF`，避免透明像素造成黑邊。

支援：

- 虛擬桌面（所有螢幕聯集）。
- 指定螢幕。
- 任意矩形（會先與虛擬桌面範圍交集）。
- 視窗可見邊界。
- 若 `CaptureCursor` 啟用，會在 `BitBlt` 後、Alpha 設定前，以 `GetCursorInfo`／`GetIconInfo`／`DrawIconEx` 將目前游標繪入記憶體 DC；支援 hotspot、隱藏游標與超出擷取範圍時略過。

`WindowsGraphicsCaptureService` 提供目前視窗的優先擷取來源：

1. Windows 10 1903（build 18362）以上且 `GraphicsCaptureSession.IsSupported()` 才啟用。
2. 以 `IGraphicsCaptureItemInterop.CreateForWindow` 從 HWND 建立擷取目標。
3. 建立支援 BGRA 的 Direct3D 11 裝置與 free-threaded frame pool，取得第一個 BGRA8 frame。
4. 透過 `SoftwareBitmap` 複製像素到 `SKBitmap`，等待 frame 最多 5 秒。
5. 初始化、GPU、逾時或目標失效時由 `CaptureCoordinator` 記錄警告並退回 GDI 可見畫面擷取。

限制：GDI 擷取的是桌面可見畫面；Windows.Graphics.Capture 仍可能受 DRM、獨佔全螢幕、最小化視窗、GPU／遠端環境影響。

### 4.2 螢幕錄影

`WindowsGraphicsRecordingService` 使用 Windows.Graphics.Capture 與 Windows Media：

1. `WindowEnumerationService` 列出目前可見且可擷取的外部視窗；全螢幕錄影使用 `MonitorService` 取得游標所在螢幕的 HMONITOR。
2. 視窗使用 `CreateForWindow`、全螢幕使用 `CreateForMonitor` 建立固定尺寸的 `GraphicsCaptureItem` 與 5-buffer free-threaded frame pool。
3. 以容量 1 的 latest-frame slot 丟棄尚未送出的舊 frame；程式最多保留 4 個 WGC capture frame，永遠替 5-buffer frame pool 保留一格，避免編碼器延後 `Processed` callback 時讓 WGC 停止更新。
4. `MediaStreamSource` 將 BGRA8 `IDirect3DSurface` 直接交給 `MediaTranscoder`；啟用系統音訊時，NAudio 以 WASAPI loopback 擷取預設播放裝置，轉為 48 kHz、雙聲道、16-bit PCM 後送入第二條 media stream。輸出為 H.264 MP4，音訊編為 192 kbps AAC。
5. Transcoder 準備完成後執行可選的 0／5／10／20／30 秒浮動倒數；倒數結束並重新驗證 HWND／Process ID 或 HMONITOR 後才呼叫 `StartCapture()`，因此倒數與準備時間不計入影片。
6. 靜態視窗不會持續產生 WGC frame，因此保留目前 surface 並重複送樣本；每個 frame 以引用計數保留到所有 `MediaStreamSample.Processed` 回呼完成。視訊 PTS 由 frame index 與 FPS 計算，錄影時鐘只控制送樣本時機。
7. 錄影中控制列由 `RecordingOverlayWindow` 顯示在目標螢幕右上角，使用 `SetWindowDisplayAffinity` 排除擷取，並以 `WS_EX_NOACTIVATE` 避免搶焦點。
8. 音訊與視訊 sample request 各自由獨立 gate 排序；等待 frame、排程時間或暫停 gate 超過 500 ms 時呼叫 `ReportSampleProgress`。暫停期間 loopback 資料會被丟棄，繼續後清空舊音訊並從原時間軸接續；停止或 EOS 會釋放等待中的請求。
9. 正常停止向 `MediaStreamSource` 回傳 EOS 並等待 `TranscodeAsync` 完成，確保 MP4 metadata 寫入；成功後才將 `.recording.tmp` 搬移為 `.mp4`。
10. 目標關閉或尺寸改變會自動停止；目前不做縮放、letterbox 或分段錄影。
11. 開始前檢查至少 256 MB 可用空間；錄影中每 5 秒檢查，低於 64 MB 以錯誤 EOS 停止。啟動時清除預設與目前設定資料夾內殘留的 `.recording.tmp`。

### 4.3 螢幕與 DPI

`MonitorService` 透過 `EnumDisplayMonitors` 與 `GetMonitorInfo` 取得：

- 裝置名稱、螢幕邊界、工作區。
- 主要螢幕旗標。
- 有效 DPI（`GetDpiForMonitor`，`MDT_EFFECTIVE_DPI`）。

螢幕依 `X`、`Y` 排序，方便建立覆蓋層與選擇目前螢幕。

### 4.4 前景視窗

- `ForegroundWindowTracker` 註冊 `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`，並忽略本行程事件。
- 觸發視窗擷取時優先取得目前前景視窗；若失敗則回退到最近記住的可見視窗；最近目標失效時再以 `EnumWindows` 依 Z-order 尋找 Clippings 後面最上層的可擷取視窗（略過 tool window、無標題與過小視窗）。
- `WindowHelper` 使用 DWM 擴充框與可見性檢查，排除最小化、隱藏、cloaked 的目標，以及系統匣、系統匣溢出、桌面、工作檢視等 shell 視窗（例如 `TopLevelWindowForOverflowXamlIsland`、`NotifyIconOverflowWindow`）。

### 4.5 影像處理

`ImageCodec`：

- `Encode(SKBitmap, ImageFormat, quality)`：PNG 或 JPEG。
- `Decode`／`TryDecode`／`Load`。
- `Crop`：最近鄰裁切，裁切範圍會先限制在影像內。
- `RotateFlip`：90 度旋轉（順／逆時針）與水平／垂直翻轉，最近鄰取樣像素精確；旋轉時輸出寬高互換。
- `EncodeThumbnailJpeg`：最長邊 320 px、JPEG 品質 82，白色背景平整化。
- `CreateThumbnail`：線性取樣縮圖。

### 4.6 資料儲存

`ClippingsStore` 使用 EF Core 與 SQLite：

- 資料庫路徑：`%LOCALAPPDATA%\Clippings\clippings.db`。
- 啟動時 `EnsureCreated()`，並啟用 `PRAGMA journal_mode=WAL`。
- 連線啟用 `Pooling=True`，每次開啟時套用 `synchronous=NORMAL`、`temp_store=MEMORY`、`cache_size=-8192`。
- 圖檔與縮圖先寫入磁碟，再寫入資料庫；刪除資料列時嘗試刪除對應檔案。
- `TrimCapturesAsync`／`TrimClipboardItemsAsync` 同時處理「超過天數」與「超過數量」，收藏／釘選項目不受影響。
- 剪貼簿以 `Hash` 唯一索引去重，重複時更新 `CreatedAt`。

### 4.7 設定

`JsonSettingsStore`：

- 使用 `System.Text.Json`，camelCase、縮排、列舉以字串表示。
- 寫入時先寫 `.tmp` 再 `File.Move(overwrite: true)`，降低設定檔損壞風險。
- 讀取失敗（JSON 格式、I/O、權限）時回到預設 `AppSettings`。

### 4.8 開機啟動

`StartupRegistrar` 寫入目前使用者登錄：

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
  Clippings = "<exe path>" --autostart
```

不需要系統管理員權限。

### 4.9 單一實例

`SingleInstanceService`：

- `Local\Clippings.SingleInstance.7F2A` Mutex 判斷是否為第一個實例。
- Named Pipe `Clippings.Activation.7F2A` 轉送啟動參數。
- 訊息格式：UTF-8 JSON 字串陣列 + `\n`，上限 1024 bytes。
- 無效或舊版訊息會降級為空參數，由第一個實例顯示主視窗。
- 事件 `ActivationRequested` 携带 `ActivationRequestedEventArgs.Arguments`。

### 4.10 主控台輸出

`ConsoleBridge.TryAttachToParent()` 使用 `AttachConsole(ATTACH_PARENT_PROCESS)`，讓 `WinExe` 在從終端機啟動時能輸出 `--help` 與參數錯誤；附加失敗時由 App 改用 `MessageBox`。

---

## 5. App 層

### 5.1 啟動流程

`App.OnStartup` 的順序：

1. 解析命令列參數。
2. `--help` 或無效參數：輸出後結束（0／2）。
3. 建立 `SingleInstanceService`；非第一個實例則轉送參數並結束（0／1）。
4. 建立 Host 與 DI 容器，啟動日誌、設定、儲存等服務。
5. 套用主題、建立系統匣、啟動剪貼簿監聽、同步開機啟動設定。
6. 建立 `MainWindow`，依參數決定是否顯示。
7. 註冊全域熱鍵、啟動單一實例監聽、執行冷啟動命令。

### 5.2 CaptureCoordinator

負責單一擷取流程：

- `_busy` 防止重入。
- 只有主視窗可見或螢幕標註進行中時，才隱藏並等待 180 ms 讓畫面穩定。
- 延遲模式先顯示倒數視窗，再轉為區域框選。
- 區域框選會為每個螢幕建立 `CaptureOverlayWindow`，並將虛擬桌面影像裁切成各螢幕影像。
- 目前視窗在能力可用時優先走 `WindowsGraphicsCaptureService`，失敗時退回 `ScreenCaptureService`；Windows 10 2004 以上以 `IsCursorCaptureEnabled` 控制游標，較舊系統（19041 以下）一律改用 GDI 備援以確保游標設定一致。
- `MainWindow` 注入 `ScreenAnnotationService`，側欄「螢幕標註」按鈕呼叫 `Toggle()`，並訂閱 `SessionChanged` 同步「螢幕標註」／「結束標註」文字與 ToolTip。
- 完成後先依動作與設定複製到剪貼簿，再編碼 PNG 與縮圖、計算 SHA-256、寫入歷史、執行保留策略。
- 最後依動作與設定執行另存、自動存檔與開啟編輯器。
- `CaptureInvocationSource`：`UserInterface`、`GlobalHotkey`、`CommandLine`、`QuickCapture`。
- 只有 `GlobalHotkey` 會播放提示音；`GlobalHotkey`、`QuickCapture` 或使用者於框選按「確定」時會開啟編輯器。

### 5.3 RecordingCoordinator

- 與圖片用 `CaptureCoordinator` 分離，狀態為 `Idle`、`Starting`、`Recording`、`Stopping`。
- 從 `ForegroundWindowTracker` 取得目標，依 `AppSettings.Recording` 建立輸出檔，同秒重複時加流水號。
- 主視窗與系統匣依狀態切換開始／停止；主視窗以 `DispatcherTimer` 顯示經過時間，完成後可開啟影片或資料夾。
- 停止、目標關閉、尺寸改變或程式結束都會等待 MP4 完檔；錯誤透過日誌與系統匣通知回報。

### 5.4 全域熱鍵

`GlobalHotkeyService`：

- 建立訊息-only `HwndSource`。
- `RegisterHotKey` 加上 `MOD_NOREPEAT`。
- 以遞增 ID 維護 callback 字典。
- `WM_HOTKEY` 時分派對應動作。
- `SuppressDispatch()` 可在錄製快捷鍵時暫停分派（設定頁使用）。
- 設定變更時 `UnregisterAll()` 後重新註冊。

### 5.5 剪貼簿監聽

`ClipboardWatcher`：

- 建立訊息-only `HwndSource`，註冊 `AddClipboardFormatListener`。
- 收到 `WM_CLIPBOARDUPDATE` 後以 260 ms `DispatcherTimer` 去抖。
- 只處理圖片內容；文字與檔案清單不記錄。
- 先以原始像素計算 SHA-256；與上一次相同時直接略過，不進行編碼。
- 圖片在背景執行緒編碼 PNG 與縮圖，再切回 UI 執行緒觸發 `Captured`。
- 可透過 `SetEnabled` 動態開啟／關閉。

### 5.6 編輯器

`EditorWindow`：

- 以 `SKElement` 繪製目前影像與標註。
- 工具列為分段式設計（`EditorToolGroupStyle`／`EditorToolButtonStyle`），依移動、標註、形狀、筆刷、編輯分組；工具本身為同一 `GroupName` 的 `RadioButton`，使用中狀態以強調色填色。工具列使用 `WrapPanel`，視窗變窄時以群組為單位換行；橡皮擦、裁切、右轉、左轉、垂直翻轉、水平翻轉都位於工具列中。
- 橡皮擦沿用 `StrokeHitTester` 整筆擦除，一次拖曳合併為單一復原步驟；容許誤差為 `Math.Max(8f, 線寬 * 2f)`。再次點擊使用中的橡皮擦或裁切，會回到切換前的工具。
- 每次開啟固定以平移起始、圖片以 80% 置中顯示，不記住上次使用的工具；復原、重做與符合視窗為純圖示按鈕。
- 編輯器 `SKElement.IgnorePixelScaling=False`，於 PaintSurface 明確套用螢幕 DPI，再套用 DIP 平移與邏輯縮放；輸入／文字／裁切覆蓋層維持 DIP。取樣依 `_zoom × DpiScaleX` 判斷，實際比例 ≥ 1 使用 nearest，縮小使用 linear。「1:1」設定 `_zoom=1/DpiScaleX` 並將置中原點對齊實體像素；比例文字仍表示邏輯縮放，ToolTip 顯示實際像素縮放。
- 平移游標預設為一般箭頭，按下按鍵開始平移時顯示四向移動游標，放開後還原。
- 標註以 `Annotation` 清單保存，繪製時才合成。
- 復原／重做使用 `UndoStack<EditorSnapshot>`，快照包含影像與標註，容量 50。還原時比較目前影像與快照影像是否為同一物件：相同代表只還原標註，保留目前縮放與位移；不同代表裁切或旋轉，重新 `FitCanvas`。
- 裁切採「先預覽、確認後才套用」，可取消；框選範圍時「複製」只輸出選取範圍（合成後以 `ImageCodec.Crop` 裁出，非破壞性）。
- 旋轉／翻轉立即套用；套用前若有標註會先合併進影像（透明度保留），單一復原步驟即可回到變形前。
- 文字輸入以覆蓋的 `TextBox` 實作，`Enter` 換行、`Ctrl+Enter` 完成、`Esc` 取消。
- 馬賽克以區塊平均色繪製，並使用快取避免重複計算。
- 輸出時合成為 `SKBitmap`，再交由 `ImageCodec` 編碼；複製與另存新檔一律包含註解（複製整張與框選複製皆先合成再輸出）。「另存新檔」僅匯出圖檔，不清除變更狀態。
- 「儲存」按鈕呼叫 `SaveToLibraryAndCloseAsync`，於背景合成後以 `UpdateCaptureImageAsync` 覆蓋原始擷取項目（原項目不在時退回 `AddCaptureAsync` 新增一筆）；成功後刷新歷史庫並關閉，失敗顯示錯誤並留在編輯器。沒有變更且原項目已存在時直接關閉，不更新時間與排序。若編輯器是從新擷圖開啟（`canDiscard` 為 `true`），儲存成功後會呼叫 `App.ShowRecentCaptures()` 顯示主視窗並切到最近擷取頁；從最近擷取重新編輯時則維持原本的刷新行為。
- 「關閉」與視窗 X 直接關閉，不儲存編輯；新擷取的「捨棄」會先刪除該筆紀錄與圖檔，成功後同樣呼叫 `App.ShowRecentCaptures()` 回到最近擷取頁。關閉事件不再自動存回。
- 鍵盤：`Ctrl+Z`／`Ctrl+Y`／`Ctrl+C`／`Ctrl+S`（儲存）、`Ctrl+Shift+S`（另存新檔）、`Ctrl++`／`Ctrl+-` 縮放、`Ctrl+0` 符合視窗、`Esc` 關閉或取消裁切、`Enter` 確認裁切。

### 5.7 框選覆蓋層

`CaptureOverlayWindow`：

- 每個螢幕一個無邊框、透明、Topmost 視窗。
- 顯示凍結的螢幕影像，支援拖曳建立選取區、移動、八方向調整大小；按住 `Shift` 拖曳固定正方形，方向鍵可微調選取區。
- 放大鏡顯示座標、RGB 與目前顏色。
- 操作列提供確定、取消；取消會清除選取並回到框選前狀態。
- 鍵盤：`Enter`／`Ctrl+E` 確定；`Esc` 先清除選取，無選取時離開擷取。
- 全域快捷鍵來源取消擷取時不恢復主視窗，其他來源仍會恢復原本可見的主視窗。
- 完成時回傳 `OverlayResult`（影像、`PixelRect`、`OverlayAction`）。

### 5.8 主題與樣式

- `ThemeService.Apply` 依 `AppTheme` 載入 `Styles/*.xaml`，插入 `Application.Resources.MergedDictionaries`。
- `System` 會讀取 `HKCU\...\Themes\Personalize\AppsUseLightTheme`。
- 套用主題時同步更新所有視窗的 DWM 深色標題列。
- 共用樣式集中於 `Styles/Controls.xaml`，顏色與筆刷使用 `DynamicResource`。
- 對比度 token：`ControlBorderBrush`（控制項邊界 ≥ 3:1）、`CardBorderBrush`（卡片與側欄分隔）、`AccentForegroundBrush`（Accent 填色上的文字／圖示）。淺色主題的 `TextSecondaryBrush` 與 Primary hover 也已調整至 WCAG AA。

### 5.9 日誌與例外

- `FileLoggerProvider` 使用容量 2048 的有界 `Channel<LogEntry>`（滿載時丟棄最舊項目），以批次方式寫入 `logs/app-yyyyMMdd.log`。
- EF Core 的 `Database.Command` 日誌過濾為 `Warning`，避免每個查詢都寫入檔案。
- 保留 7 天，啟動時清除過期檔案。
- UI、背景與未觀察工作例外都有全域處理常式。

---

## 6. 資料格式

### 6.1 SQLite

`Captures`

| 欄位 | 型別 | 備註 |
| --- | --- | --- |
| Id | INTEGER | PK |
| CreatedAt | TEXT | UTC |
| Width／Height | INTEGER | 像素 |
| FilePath | TEXT(1024) | PNG 路徑 |
| ThumbnailPath | TEXT(1024) | JPEG 路徑 |
| Hash | TEXT | SHA-256 |
| IsFavorite | INTEGER | 收藏 |
| Source | TEXT(128) | 例如「區域擷取」 |

索引：`CreatedAt`、`Hash`、`(IsFavorite, CreatedAt)`。

`ClipboardItems`

| 欄位 | 型別 | 備註 |
| --- | --- | --- |
| Id | INTEGER | PK |
| CreatedAt | TEXT | UTC |
| Kind | INTEGER | 文字／圖片／檔案 |
| TextContent | TEXT | 文字或檔案路徑清單 |
| FilePath | TEXT | 圖片路徑 |
| ThumbnailPath | TEXT | 縮圖路徑 |
| Hash | TEXT(64) | 唯一索引 |
| IsPinned | INTEGER | 釘選 |

索引：`Hash`（唯一）、`CreatedAt`、`(IsPinned, CreatedAt)`。

### 6.2 設定檔

`settings.json` 範例：

```json
{
  "hotkeyRegion": "Ctrl+Shift+X",
  "hotkeyFullScreen": "Ctrl+Shift+F",
  "hotkeyWindow": "Ctrl+Shift+W",
  "hotkeyDelayed": "Ctrl+Shift+D",
  "delaySeconds": 5,
  "saveDirectory": "",
  "defaultFormat": "Png",
  "jpegQuality": 90,
  "autoCopy": true,
  "playCaptureSound": true,
  "saveAutomatically": false,
  "clipboardHistoryEnabled": true,
  "maxCaptureItems": 200,
  "maxClipboardItems": 200,
  "retentionDays": 30,
  "theme": "System",
  "startWithWindows": false,
  "showMagnifier": true
}
```

### 6.3 檔名規則

```
Clippings_yyyyMMdd_HHmmss_<hash 前 8 碼>.png
```

剪貼簿縮圖加上 `clip_` 前綴。

---

## 7. 命令列

`CommandLineOptions.TryParse` 支援：

- `--show`
- `--capture-region`
- `--capture-full`
- `--capture-monitor`
- `--capture-window`
- `--autostart`
- `--help`

規則：

- 不分大小寫、忽略前後空白。
- `--help` 優先於其他參數。
- 一次只能指定一個指令，重複視為錯誤。
- `HideMainWindow` 在擷取指令或「只有 `--autostart`」時為 `true`。

退出碼：

| 代碼 | 意義 |
| --- | --- |
| 0 | 成功啟動或成功轉送 |
| 1 | 無法連線到既有實例 |
| 2 | 參數錯誤 |

---

## 8. 測試

```powershell
dotnet test Clippings.sln
dotnet test Clippings.sln --filter FullyQualifiedName~CommandLineOptionsTests
```

目前測試涵蓋：

- 快捷鍵解析、顯示名稱往返。
- `PixelRect` 幾何運算。
- `UndoStack` 復原／重做與容量。
- 設定複製、預設值、列舉穩定性。
- 雜湊與檔名規則。
- 命令列參數解析。

UI、GDI 擷取與實際剪貼簿行為需在 Windows 桌面環境手動驗證。

---

## 9. 開發慣例

- 4 空格縮排；公開成員 PascalCase、區域變數 camelCase、私有欄位 `_camelCase`。
- 非同步方法以 `Async` 結尾；函式庫層使用 `ConfigureAwait(false)`，UI 層不使用。
- 不自訂 `INotifyPropertyChanged`，使用 `CommunityToolkit.Mvvm`。
- 使用者可見文字集中於 `AppStrings`（後續遷移到 `.resx`）。
- 不新增未在 `Directory.Packages.props` 宣告的套件。
- 啟用可空性與 `TreatWarningsAsErrors`，不關閉警告。
- 完成功能後執行 `dotnet build`、`dotnet test`、`dotnet format --verify-no-changes`。
- 每次開發更新 `docs/DEV_LOG.md`。

---

## 10. 效能與限制

- 全螢幕擷取會一次配置整個虛擬桌面的點陣圖；多個 4K 螢幕會佔用可觀記憶體。
- 縮圖最長邊 320 px，並以檔案時間戳快取，降低歷史頁重複解碼成本。
- 歷史頁每頁載入 60 筆；剪貼簿頁使用預設虛擬化清單。
- 剪貼簿圖片先比對原始像素雜湊，重複內容不重新編碼。
- 歷史查詢上限 5000 筆，搜尋使用 SQL `LIKE`。
- 保留策略在每次新增後執行，刪除時同步移除檔案。
- GDI 擷取、單螢幕框選與混合 DPI 限制如前所述。
