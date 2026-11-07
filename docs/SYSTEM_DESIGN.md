# 系統設計文件

本文件說明 Clippings 的設計目標、需求、模組責任、關鍵決策與取捨。若需要程式碼層級細節，請見 [技術文件](TECHNICAL.md)；若需要元件與資料流圖，請見 [系統架構](ARCHITECTURE.md)。

---

## 1. 範圍

Clippings 是一個 Windows 單機桌面應用程式，提供：

- 多種模式的螢幕擷取。
- 框選與標註編輯。
- 擷取歷史與剪貼簿歷史。
- 系統匣常駐、全域快捷鍵、開機啟動與單一實例。
- 最小命令列介面。

本文件不涵蓋雲端同步、多人協作、行動裝置版本或影像辨識等未實作功能。

---

## 2. 設計目標

| 目標 | 說明 |
| --- | --- |
| 快速 | 從按下快捷鍵到可框選的時間要短，不打斷原本工作流程 |
| 直覺 | 框選後直接進入編輯器，複製與儲存設定集中處理 |
| 可靠 | 擷取失敗要有提示，資料寫入失敗不能讓程式崩潰 |
| 隱私 | 所有資料留在本機，無網路功能 |
| 可攜 | 免安裝、self-contained，資料集中在單一資料夾 |
| 可維護 | Core 與平台／UI 分離，核心邏輯可單元測試 |

### 非目標

- 取代專業影像編輯器。
- 提供雲端備份或跨裝置同步。
- 支援 Windows 10 1809 以前的版本。
- 支援 ARM64 或 32 位元 Windows。
- 完整錄影功能（螢幕／區域、音訊、暫停、影片編輯）、滾動長截圖、OCR。

---

## 3. 使用情境

| 編號 | 情境 | 主要流程 |
| --- | --- | --- |
| UC-01 | 快速截取畫面片段 | 快捷鍵 → 框選 → 複製或編輯 |
| UC-02 | 截取整個畫面或指定螢幕 | 快捷鍵／系統匣／命令列 → 直接儲存與複製 |
| UC-03 | 截取目前視窗 | 快捷鍵 → 取得前景視窗邊界 → 擷取 |
| UC-04 | 延遲截取開啟中的選單 | 快捷鍵 → 倒數 → 框選 |
| UC-05 | 在截圖上標註 | 編輯器 → 箭頭／文字／馬賽克 → 複製、另存新檔或儲存 |
| UC-06 | 找回稍早的截圖 | 主視窗 → 最近擷取 → 搜尋／收藏／重新編輯 |
| UC-07 | 找回稍早複製的圖片 | 主視窗 → 剪貼簿歷史 → 釘選／複製／加入擷取 |
| UC-08 | 由腳本觸發擷取 | 命令列參數 → 轉送既有實例 → 擷取 |
| UC-09 | 開機後自動常駐 | `--autostart` → 隱藏主視窗 → 註冊快捷鍵 |
| UC-10 | 錄製目前視窗 | 系統匣開始 → WGC 擷取 → 系統匣停止／目標改變 → 無聲 MP4 |

---

## 4. 需求

### 4.1 功能需求

| 編號 | 需求 | 對應實作 |
| --- | --- | --- |
| FR-01 | 全域快捷鍵可觸發區域、全螢幕、視窗、延遲擷取 | `GlobalHotkeyService`、`CaptureCoordinator` |
| FR-02 | 快捷鍵可在設定頁修改並即時重新註冊 | `SettingsPage`、`App.ApplyHotkeys` |
| FR-03 | 區域擷取需先凍結畫面，支援移動與調整選取範圍 | `CaptureOverlayWindow` |
| FR-04 | 框選時顯示放大鏡、座標、RGB 與尺寸 | `CaptureOverlayWindow` |
| FR-05 | 提供確定、取消兩種框選完成動作 | `OverlayAction` |
| FR-06 | 編輯器支援多種標註工具、16 色與線寬 | `EditorWindow`、`Annotation` |
| FR-07 | 編輯器支援復原、重做、可取消的裁切與旋轉／翻轉 | `UndoStack`、`EditorSnapshot`、`ImageCodec` |
| FR-08 | 擷取結果寫入歷史庫並可搜尋、收藏、刪除 | `ClippingsStore`、`HistoryPage` |
| FR-09 | 剪貼簿歷史記錄圖片並支援去重與加入最近擷取 | `ClipboardWatcher`、`ClippingsStore` |
| FR-10 | 剪貼簿項目可釘選，不受保留策略影響 | `IsPinned` |
| FR-11 | 歷史庫與剪貼簿可設定數量與天數上限 | `TrimCapturesAsync`、`TrimClipboardItemsAsync` |
| FR-12 | 支援 PNG／JPEG 輸出與 JPEG 品質調整 | `ImageCodec`、`AppSettings` |
| FR-13 | 系統匣常駐、右鍵選單、關閉視窗不結束程式 | `App`、`TaskbarIcon` |
| FR-14 | 單一實例，重複啟動可喚出既有視窗 | `SingleInstanceService` |
| FR-15 | 支援開機啟動且不顯示主視窗 | `StartupRegistrar`、`--autostart` |
| FR-16 | 支援最小命令列參數與退出碼 | `CommandLineOptions`、`App` |
| FR-17 | 提供九種介面主題 | `ThemeService`、`Styles/*.xaml` |
| FR-18 | 編輯器按「儲存」時把變更覆蓋存回原本的擷取項目 | `EditorWindow`、`UpdateCaptureImageAsync` |
| FR-19 | 從主視窗或系統匣選擇視窗或目前螢幕錄影，設定 FPS／位元率／游標／延遲／資料夾並輸出無聲 H.264 MP4；錄影中可暫停、繼續、停止 | `RecordingCoordinator`、`RecordingOverlayService`、`WindowsGraphicsRecordingService`、`SettingsPage` |

### 4.2 非功能需求

| 類別 | 要求 |
| --- | --- |
| 效能 | 一般 1080p 螢幕的擷取與編碼不應讓 UI 明顯凍結；影像工作在背景執行緒 |
| 可靠性 | I/O 失敗不可導致崩潰；設定檔損壞時回到預設值 |
| 隱私 | 無網路連線、無遙測；資料只存本機 |
| 相容性 | Windows 10 1809 以上、x64 |
| 可及性 | 可點擊圖示按鈕提供 `AutomationProperties.Name` 與 ToolTip |
| 可維護性 | Core 不依賴 UI／Win32；核心邏輯有單元測試 |
| 可攜性 | 提供 self-contained 免安裝版與 framework-dependent 精簡版，資料集中於 `%LOCALAPPDATA%\Clippings` |

---

## 5. 系統邊界

```mermaid
flowchart LR
    USER["使用者"] --> UI["Clippings WPF"]
    SCRIPT["腳本／捷徑"] --> CLI["Clippings.exe 命令列"]
    CLI --> UI
    UI --> WIN["Windows API<br/>GDI、Hotkey、Clipboard、DWM"]
    UI --> FS["本機檔案系統<br/>%LOCALAPPDATA%"]
    UI --> REG["HKCU Run 登錄值"]
    UI -.->|"無網路"| NET["（不連線）"]
```

系統只與 Windows 本機 API 和檔案系統互動，沒有伺服器端元件。

---

## 6. 模組設計

| 模組 | 責任 | 不負責 |
| --- | --- | --- |
| Core.Models | 資料結構與設定 | I/O、UI |
| Core.Editing | 幾何與復原堆疊 | 實際繪圖 |
| Core.Input | 快捷鍵格式解析 | 註冊熱鍵 |
| Core.Startup | 命令列解析 | 啟動流程 |
| Platform.Capture | 螢幕像素取得 | 框選 UI |
| Platform.Imaging | 編碼、解碼、縮圖、裁切、旋轉／翻轉 | 標註合成 |
| Platform.Storage | SQLite、檔案、設定、登錄 | 商業流程 |
| Platform.SingleInstance | Mutex 與管線轉送 | 參數解讀 |
| App.Services | 流程協調、監聽、熱鍵、主題 | 平台細節 |
| App.Views | 視窗、頁面、互動 | 資料持久化 |

---

## 7. 關鍵設計決策

### 7.1 WPF 而非 WinUI 3

- 本專案依使用者決定採用 WPF。
- 優點：成熟、SkiaSharp 有現成 WPF 控制項、`H.NotifyIcon` 系統匣支援完整。
- 缺點：Fluent 視覺需自行以樣式模擬，沒有 WinUI 的原生 NavigationView。
- 決策記錄於 `docs/DEV_LOG.md`。

### 7.2 GDI `BitBlt` 搭配 Windows.Graphics.Capture

- 區域、全螢幕與螢幕擷取維持 GDI，保留高相容性與直接取得虛擬桌面的能力。
- 目前視窗在 Windows 10 1903 以上優先使用 Windows.Graphics.Capture，補強被其他視窗遮擋的情境。
- Windows.Graphics.Capture 不支援或失敗時自動退回 GDI，不提高應用程式的最低 Windows 版本。

### 7.3 SQLite 中繼資料 + 外部圖檔

- 不在資料庫存放大型 BLOB，避免資料庫膨脹與備份困難。
- 原圖與縮圖以檔案保存，刪除資料列時同步刪除檔案。
- 縮圖最長邊 320 px，降低歷史頁載入成本。

### 7.4 JSON 設定而非資料庫

- 設定項目少、需要人工檢視與備份。
- 使用暫存檔 + 原子搬移降低損壞風險。
- 損壞時回到預設值，不阻止程式啟動。

### 7.5 單一實例使用 Mutex + Named Pipe

- Mutex 判斷是否已有實例。
- Named Pipe 轉送參數，讓第二次啟動能觸發既有實例。
- JSON 單行訊息方便擴充，並以 1024 bytes 上限避免惡意過長內容。

### 7.6 剪貼簿去抖與去重

- `WM_CLIPBOARDUPDATE` 可能連續觸發，260 ms 去抖可避免重複處理。
- SHA-256 去重避免相同內容洗版；同時保留更新時間。

### 7.7 標註以資料模型保存

- 工具列操作只新增 `Annotation`，不直接修改像素。
- 復原／重做以快照實作，裁切與旋轉／翻轉才真正改變底圖。
- 輸出時一次合成，方便維持品質與簡化互動。

### 7.8 命令列採「轉送」而非第二個常駐程式

- 保持單一實例與一致的資料狀態。
- 第二實例只負責轉送並結束，退出碼代表轉送結果。

---

## 8. 狀態與生命週期

### 8.1 擷取狀態

```mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> Capturing: 快捷鍵／系統匣／命令列
    Capturing --> Countdown: 延遲模式
    Countdown --> Selecting: 倒數結束
    Capturing --> Selecting: 區域模式
    Selecting --> Saving: 完成框選
    Selecting --> Idle: 取消
    Capturing --> Saving: 全螢幕／螢幕／視窗
    Saving --> Editing: 使用者選編輯或快捷鍵來源
    Saving --> Idle: 複製／自動存檔完成
    Editing --> Idle: 關閉編輯器
```

- 同一時間只允許一個擷取流程；忙碌時新請求會被忽略。
- 失敗時顯示提示並回到 `Idle`。

### 8.2 應用程式狀態

```mermaid
stateDiagram-v2
    [*] --> Starting
    Starting --> TrayHidden: --autostart 或擷取命令
    Starting --> MainVisible: 一般啟動
    MainVisible --> TrayHidden: 關閉視窗
    TrayHidden --> MainVisible: 系統匣左鍵／--show
    TrayHidden --> TrayHidden: 快捷鍵擷取
    MainVisible --> Exiting: 系統匣「結束」
    TrayHidden --> Exiting: 系統匣「結束」
    Exiting --> [*]
```

`ShutdownMode` 設為 `OnExplicitShutdown`，因此關閉主視窗只會隱藏，不會結束程式。

---

## 9. 錯誤處理策略

| 情境 | 處理方式 |
| --- | --- |
| 快捷鍵被佔用 | 記錄警告、系統匣通知、設定頁顯示失敗清單 |
| 螢幕擷取失敗 | 對話框提示，流程回到閒置 |
| 找不到前景視窗 | 拋出可預期例外，提示使用者 |
| 歷史庫寫入失敗 | 記錄錯誤、系統匣通知；已完成的影像仍可複製 |
| 自動存檔失敗 | 記錄警告、通知；不影響歷史庫 |
| 剪貼簿開啟失敗 | 通知使用者剪貼簿可能被佔用 |
| 設定檔損壞 | 回到預設設定，不阻止啟動 |
| 命令列參數錯誤 | 輸出用法並以退出碼 2 結束 |
| 單一實例轉送失敗 | 輸出錯誤並以退出碼 1 結束 |
| 未處理例外 | 寫入日誌；UI 例外顯示對話框 |

---

## 10. 資料保留與一致性

- 每次新增擷取或剪貼簿項目後執行保留策略。
- 保留策略同時考慮天數與數量，兩者取聯集後刪除。
- 收藏／釘選項目永遠保留。
- 刪除資料列後才刪除檔案；若檔案刪除失敗（被鎖定），只留下孤兒檔案，不影響資料庫。
- 剪貼簿以唯一雜湊避免重複資料列。
- SQLite 使用 WAL；建議備份前先結束程式。

---

## 11. 安全與隱私

- 無網路功能、無遙測、無帳號。
- 剪貼簿監聽預設開啟，可關閉。
- 日誌只保留 7 天，不含剪貼簿內容（除非例外訊息本身包含）。
- 擷取結果可能包含敏感資訊，使用者應自行管理 `%LOCALAPPDATA%\Clippings` 與自動存檔資料夾。
- 開機啟動只寫入目前使用者的 `HKCU`，不需要系統管理員權限。

---

## 12. 測試策略

| 層級 | 方式 |
| --- | --- |
| Core | xUnit 單元測試：快捷鍵、幾何、復原堆疊、設定、雜湊、命令列 |
| Platform | 以手動或整合測試驗證 Windows.Graphics.Capture、GDI、SQLite、管線 |
| UI | 手動操作驗證框選、編輯器、設定、系統匣 |
| 建置 | `dotnet build`、`dotnet test`、`dotnet format --verify-no-changes` |

目前沒有自動化 UI 測試與實際螢幕擷取測試，這是後續可改善之處。

---

## 13. 風險與限制

| 風險 | 影響 | 緩解 |
| --- | --- | --- |
| WGC／GDI 擷取受 DRM、獨佔全螢幕或 GPU 環境影響 | 黑畫面、空白或失敗 | WGC 失敗自動退回 GDI；文件說明仍無法擷取的限制 |
| 混合 DPI 多螢幕 | 框選位置誤差 | 目前僅以 100% 實測；需補齊驗證 |
| 全螢幕擷取記憶體用量 | 多 4K 螢幕可能佔用大量記憶體 | 完成後立即釋放；縮圖降低長期成本 |
| 剪貼簿含敏感資料 | 隱私風險 | 可關閉監聽、可清除、資料只存本機 |
| 系統匣圖示快取 | 舊圖示顯示 | 文件提供重新固定步驟 |
| 無自動化 UI 測試 | 回歸風險 | 以 Core 測試與手動清單補強 |

---

## 14. 後續規劃

- 擴充錄影至整個螢幕／區域、音訊、暫停與 GPU 固定畫布縮放。
- 跨螢幕框選。
- 滾動長截圖。
- 多語系資源檔（`.resx`）。
- 自動化 UI 測試與擷取整合測試。
- 命令列 `--wait` 或輸出路徑，讓腳本能取得完成結果。
- 混合 DPI 多螢幕實測與修正。
