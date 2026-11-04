# 系統架構

本文件說明 Clippings 的整體架構、分層責任、執行時元件、主要資料流與執行緒模型。實作細節請搭配 [技術文件](TECHNICAL.md) 與 [系統設計](SYSTEM_DESIGN.md) 閱讀。

---

## 1. 架構目標

- **離線、免安裝**：不依賴雲端服務，self-contained 發佈即可執行。
- **低介入常駐**：以系統匣常駐，全域快捷鍵隨時可用。
- **可測試的核心**：與 UI、Win32 無關的邏輯放在 `Clippings.Core`，可用 xUnit 測試。
- **清楚的平台邊界**：Win32、SkiaSharp、SQLite 等平台相依程式碼集中在 `Clippings.Platform`。
- **單一實例**：重複啟動不產生第二個常駐程式，命令列參數會轉送給既有實例。

---

## 2. 分層架構

```mermaid
flowchart TB
    subgraph App["Clippings.App（WPF）"]
        UI["Views / XAML"]
        SVC["App Services<br/>CaptureCoordinator、ClipboardWatcher<br/>GlobalHotkeyService、ThemeService"]
        DI["Host / DI Container"]
        LOG["FileLoggerProvider"]
    end

    subgraph Platform["Clippings.Platform"]
        CAP["Capture / Display<br/>WGC、GDI、Monitor、DPI"]
        IMG["Imaging<br/>SkiaSharp"]
        STORE["Storage<br/>EF Core + SQLite、JSON、Run 登錄"]
        INT["Interop<br/>Win32 P/Invoke"]
        SI["SingleInstance<br/>Mutex + Named Pipe"]
    end

    subgraph Core["Clippings.Core"]
        MODEL["Models / Settings"]
        EDIT["Editing<br/>PixelRect、UndoStack"]
        HELP["Helpers<br/>Hash、Path"]
        INPUT["Input<br/>HotkeyGesture"]
        START["Startup<br/>CommandLineOptions"]
        ISTORE["IClippingsStore<br/>ISettingsStore"]
    end

    UI --> SVC
    SVC --> DI
    SVC --> Platform
    DI --> Platform
    DI --> Core
    Platform --> Core
    APPX["App.xaml.cs<br/>啟動與組裝"] --> DI
    APPX --> LOG
```

### 專案責任

| 專案 | 目標框架 | 責任 |
| --- | --- | --- |
| `Clippings.Core` | `net10.0-windows10.0.19041.0` | 純資料模型、設定、`PixelRect`、`UndoStack`、雜湊、路徑、快捷鍵格式、命令列解析、儲存介面 |
| `Clippings.Platform` | 同上 | Windows.Graphics.Capture／GDI 螢幕擷取、螢幕列舉與 DPI、SkiaSharp 編碼、EF Core/SQLite、JSON 設定、開機登錄、單一實例管線、主控台附加 |
| `Clippings.App` | 同上（`UseWPF`） | WPF 視窗與頁面、DI Host、系統匣、擷取協調、剪貼簿監聽、全域熱鍵、主題、日誌 |
| `Clippings.Core.Tests` | 同上 | `Core` 的單元測試（xUnit） |

依賴方向固定為：

```
Clippings.App ──► Clippings.Platform ──► Clippings.Core
      └──────────────────────────────────►
```

`Core` 不參考 `Platform` 或 `App`；`Platform` 不參考 `App`。

---

## 3. 執行時元件

```mermaid
flowchart LR
    TRAY["TrayIcon<br/>H.NotifyIcon"] --> APP["App"]
    HOTKEY["GlobalHotkeyService"] --> APP
    CW["ClipboardWatcher"] --> APP
    SI["SingleInstanceService"] --> APP
    APP --> COORD["CaptureCoordinator"]
    APP --> REC["RecordingCoordinator"]
    REC --> WGCR["WindowsGraphicsRecordingService"]
    WGCR --> MP4["MediaTranscoder<br/>H.264 MP4"]
    COORD --> OVERLAY["CaptureOverlayWindow<br/>每螢幕一個"]
    COORD --> EDITOR["EditorWindow"]
    COORD --> CAP["ScreenCaptureService"]
    COORD --> STORE["IClippingsStore"]
    APP --> MW["MainWindow"]
    MW --> HIST["HistoryPage"]
    MW --> CLIP["ClipboardPage"]
    MW --> SET["SettingsPage"]
    SET --> STATE["AppState"]
    HIST --> STORE
    CLIP --> STORE
    STATE --> JSON["JsonSettingsStore"]
    APP --> THEME["ThemeService"]
    APP --> NOTIFY["TrayNotificationService"]
```

| 元件 | 生命週期 | 主要職責 |
| --- | --- | --- |
| `App` | 整個程式 | 解析命令列、建立 Host、初始化系統匣與服務、處理單一實例轉送 |
| `MainWindow` | 單例，可隱藏 | 三頁導覽、錄影控制與計時、完成檔案操作、關閉時縮到系統匣 |
| `HistoryPage` | 首次導覽時建立 | 擷取歷史查詢與操作 |
| `ClipboardPage` | 首次導覽時建立 | 剪貼簿歷史查詢與操作 |
| `SettingsPage` | 首次導覽時建立 | 設定編輯、快捷鍵錄製、錄影輸出選項、儲存 |
| `CaptureCoordinator` | 單例 | 協調擷取流程、覆蓋層、儲存、複製、自動存檔與編輯器 |
| `RecordingCoordinator` | 單例 | 管理視窗／螢幕錄影、準備／倒數／錄影／暫停狀態、開始／停止、輸出路徑與通知 |
| `RecordingTargetPicker` | 單例 | 顯示可錄製視窗清單並回傳使用者選定的視窗 |
| `RecordingCountdownService` | 單例 | 在目標中央顯示不搶焦點且排除擷取的浮動倒數 |
| `RecordingOverlayService` | 單例 | 管理錄影中控制列，顯示時間、目標名稱與暫停／繼續／停止 |
| `RecordingOverlayWindow` | 每次錄影 | 目標螢幕右上角的浮動控制列 |
| `RecordingControl` | 每次錄影 | 可暫停計時器與 sample request 暫停 gate |
| `WindowsGraphicsRecordingService` | 每次錄影建立內部 session | WGC frame、latest-frame slot、MediaStreamSource、H.264 MP4 完檔 |
| `CaptureOverlayWindow` | 每次框選 | 凍結畫面、框選、放大鏡、浮動操作列 |
| `EditorWindow` | 每次編輯 | 標註、裁切、旋轉／翻轉、復原／重做、複製／儲存 |
| `GlobalHotkeyService` | 單例 | `RegisterHotKey`、訊息視窗、快捷鍵分派 |
| `ClipboardWatcher` | 單例 | `WM_CLIPBOARDUPDATE` 監聽、去抖、內容擷取 |
| `ThemeService` | 單例 | 切換資源字典與標題列深色模式 |
| `SingleInstanceService` | 單例 | Mutex 判斷、Named Pipe 轉送啟動參數 |
| `ClippingsStore` | 單例 | SQLite 與圖檔存取、去重、保留策略 |

---

## 4. 主要流程

### 4.1 區域擷取

```mermaid
sequenceDiagram
    participant HK as GlobalHotkeyService
    participant APP as App
    participant C as CaptureCoordinator
    participant S as ScreenCaptureService
    participant O as CaptureOverlayWindow
    participant E as EditorWindow
    participant DB as ClippingsStore

    HK->>APP: WM_HOTKEY（Ctrl+Shift+X）
    APP->>C: CaptureRegionAsync(GlobalHotkey)
    C->>C: 隱藏主視窗並等待 180 ms
    C->>S: CaptureVirtualScreen()
    S-->>C: SKBitmap + 虛擬桌面範圍
    C->>O: 每個螢幕建立覆蓋層
    O-->>C: 使用者完成框選（影像、範圍、動作）
    C->>C: 編碼 PNG 與 JPEG 縮圖、計算 SHA-256
    C->>DB: AddCaptureAsync + TrimCapturesAsync
    C->>C: 依設定複製到剪貼簿／自動存檔
    C->>E: 快捷鍵來源時開啟編輯器
```

重點：

- 主視窗可見時才先隱藏，避免拍到自身；未顯示時不等待隱藏動畫。
- 先擷取整個虛擬桌面，再依螢幕裁切成各自的覆蓋層影像，確保框選時畫面靜止。
- 完成後先複製到剪貼簿，再於背景執行 PNG 編碼、縮圖與歷史寫入。

### 4.2 全螢幕、目前螢幕與目前視窗

| 模式 | 擷取來源 | 後續動作 |
| --- | --- | --- |
| 全螢幕 | 所有螢幕的聯集 | 直接進入儲存與複製流程 |
| 目前螢幕 | 游標所在螢幕 | 裁切後進入儲存與複製流程 |
| 目前視窗 | Windows.Graphics.Capture；不可用時退回前景視窗可見邊界（DWM 擴充框） | 與目前螢幕相同 |
| 延遲 | 倒數後改走區域框選 | 與區域擷取相同 |

### 4.3 螢幕錄影

```mermaid
sequenceDiagram
    participant U as 主視窗／系統匣
    participant P as RecordingTargetPicker
    participant R as RecordingCoordinator
    participant W as WindowsGraphicsRecordingService
    participant G as Windows.Graphics.Capture
    participant M as MediaTranscoder
    participant O as RecordingOverlayWindow

    U->>R: 選擇視窗錄影／全螢幕錄影
    R->>P: 顯示可錄製視窗清單（視窗模式）
    P-->>R: 視窗目標
    R->>R: 建立 RecordingTarget（HWND 或 HMONITOR）
    R->>W: RecordWindowAsync／RecordMonitorAsync
    W->>G: 建立固定尺寸 frame pool
    W->>M: PrepareMediaStreamSourceTranscodeAsync
    W->>R: 執行浮動倒數（選用）
    R->>R: 重新驗證目標
    W->>G: StartCapture
    R->>O: 顯示錄影中控制列
    U->>R: 停止錄影
    R->>W: CancellationToken
    W-->>M: EOS，等待 MP4 完檔
    W-->>R: RecordingResult
```

錄影維持固定輸出尺寸；目標關閉或尺寸改變會送出 EOS。暫停時 sample request 會暫停，時間軸不前進。暫存檔完成封裝後才改名為 MP4，不寫入圖片歷史資料庫。

### 4.4 剪貼簿歷史

```mermaid
sequenceDiagram
    participant OS as Windows
    participant CW as ClipboardWatcher
    participant APP as App
    participant DB as ClippingsStore
    participant UI as ClipboardPage

    OS->>CW: WM_CLIPBOARDUPDATE
    CW->>CW: 重新啟動 260 ms 去抖計時器
    CW->>CW: 僅處理圖片（文字與檔案不記錄）
    CW->>CW: 先以原始像素計算 SHA-256，重複時不編碼
    CW->>APP: Captured(snapshot)
    APP->>DB: AddClipboardItemAsync
    APP->>DB: 超過上限 + 20 時 TrimClipboardItemsAsync
    APP->>UI: NotifyClipboardStored()
```

優先順序為檔案 → 圖片 → 文字。相同雜湊只更新時間，不新增資料列。

### 4.5 單一實例與命令列轉送

```mermaid
sequenceDiagram
    participant N as 新啟動的 Clippings
    participant M as Mutex
    participant P as Named Pipe
    participant A as 既有 Clippings
    participant C as CaptureCoordinator

    N->>M: 取得具名 Mutex
    alt 已是唯一實例
        N->>P: 寫入 JSON 參數
        N-->>N: 以退出碼 0 結束
    else 第一個實例
        A->>P: 等待連線
        P-->>A: 參數字串
        A->>A: Dispatcher 解析參數
        A->>C: 顯示視窗或觸發擷取
    end
```

### 4.6 設定變更

```mermaid
flowchart LR
    SET["SettingsPage"] --> STATE["AppState.Save"]
    STATE --> JSON["settings.json"]
    STATE --> EVT["SettingsChanged"]
    EVT --> THEME["ThemeService.Apply"]
    EVT --> CW["ClipboardWatcher.SetEnabled"]
    EVT --> RUN["StartupRegistrar.SetEnabled"]
    EVT --> HK["重新註冊全域熱鍵"]
```

---

## 5. 執行緒模型

| 工作 | 執行位置 | 說明 |
| --- | --- | --- |
| UI 操作、視窗生命週期 | WPF Dispatcher | 所有 WPF 控制項只能在此執行緒存取 |
| 螢幕擷取（GDI `BitBlt`） | `Task.Run` | 避免阻塞 UI |
| 視窗擷取（Windows.Graphics.Capture） | free-threaded frame pool | 首幀複製完成後轉為 `SKBitmap` |
| 螢幕錄影 frame | free-threaded frame pool | latest-frame slot；未送出的舊 frame 立即丟棄 |
| H.264 MP4 編碼 | MediaStreamSource／MediaTranscoder | GPU surface 樣本、正常 EOS 完檔 |
| 影像編碼、縮圖、解碼 | `Task.Run` | SkiaSharp 操作 |
| 剪貼簿圖片編碼 | `Task.Run` | 完成後再切回 Dispatcher 觸發事件 |
| 剪貼簿去抖 | `DispatcherTimer`（260 ms） | 避免短時間內重複處理 |
| 日誌寫入 | 背景 Channel 消費者 | 不阻塞呼叫端，保留 7 天 |
| Named Pipe 監聽 | 背景 `Task` | 收到後以 `Dispatcher.BeginInvoke` 切回 UI |
| 全域熱鍵與前景視窗 | 訊息視窗／WinEvent Hook | 由 Windows 訊息迴圈驅動 |

`CaptureCoordinator` 以 `_busy` 旗標避免重入；忙碌時新的擷取請求會被忽略。

---

## 6. 資料架構

```mermaid
flowchart LR
    DB["clippings.db<br/>SQLite（WAL）"] --> T1["Captures"]
    DB --> T2["ClipboardItems"]
    FS["%LOCALAPPDATA%\\Clippings"] --> IMG["images/*.png"]
    FS --> TH["thumbs/*.jpg"]
    FS --> CB["clipboard/*.png"]
    FS --> SET["settings.json"]
    FS --> LOG["logs/app-YYYYMMDD.log"]
```

- `Captures`：時間、尺寸、原圖路徑、縮圖路徑、SHA-256、收藏旗標、來源。
- `ClipboardItems`：時間、類型、文字內容、圖檔路徑、縮圖路徑、SHA-256（唯一索引）、釘選旗標。
- 圖檔以「時間戳 + 雜湊前 8 碼」命名，刪除資料列時同步刪除檔案。
- SQLite 啟用 WAL 模式；同一時間只有一個應用程式實例會寫入。

---

## 7. 錯誤處理與日誌

- 服務層以 `try/catch` 包裹可預期的 I/O 與平台錯誤，記錄後以系統匣通知或對話框告知使用者。
- UI 執行緒、背景執行緒與未觀察的工作例外都有全域處理常式。
- 日誌使用 `Microsoft.Extensions.Logging`，輸出到 `%LOCALAPPDATA%\Clippings\logs`，保留 7 天。
- 正式版不輸出主控台日誌；`--help` 與命令列錯誤才會附加父主控台。

---

## 8. 安全與隱私

- 程式沒有任何網路呼叫，也不會上傳截圖、剪貼簿或日誌。
- 所有資料位於目前使用者的 `%LOCALAPPDATA%`，受 Windows 使用者權限保護。
- 剪貼簿監聽可完全關閉。
- 受保護內容（DRM、獨佔全螢幕）可能無法擷取。
- 詳細說明見 [隱私說明](PRIVACY.md)。

---

## 9. 部署與版本

- 目標框架：`net10.0-windows10.0.19041.0`，最低支援 Windows 10 1809。
- 平台：x64。
- 發佈：win-x64，提供兩種版本：
  - 完整免安裝版：self-contained + ReadyToRun，不需要 .NET。
  - 精簡版：framework-dependent、關閉 ReadyToRun，需 .NET 10 Desktop Runtime。
- 版本集中於 `Directory.Build.props`；`Version` 採 `主.次.修`，`AssemblyVersion` 僅在主次版變更時調整。
- 詳細流程見 [建置與發佈](BUILD_AND_RELEASE.md)。

---

## 10. 設計限制

- 區域、全螢幕與螢幕擷取仍使用 GDI `BitBlt`；Windows.Graphics.Capture 目前僅用於目前視窗的單張擷取。
- 框選一次僅限單一螢幕。
- 混合 DPI 多螢幕僅以 100% 縮放實測。
- 介面文字尚未外部化為 `.resw`／`.resx`。
- WPF 與 WinUI 3 的控制項不可混用；本專案選擇 WPF 的決策記錄於 [開發紀錄](DEV_LOG.md)。
