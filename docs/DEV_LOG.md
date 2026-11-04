# 開發紀錄

最新紀錄放最上面。每次開發工作都要更新，不要事後補寫。

---

## 2026-11-04 - v1.22.0

### 目標

避免全螢幕錄影開頭錄到「已開始錄影」通知，並讓錄影控制列提供暫停／繼續與停止快捷鍵。

### 變更

- `RecordingCoordinator`：僅視窗錄影顯示「已開始錄影」通知；全螢幕錄影改由控制列提示。
- `RecordingHotkeys`：新增 `Ctrl+Shift+P` 暫停／繼續、`Ctrl+Shift+S` 停止。
- `RecordingOverlayWindow`：在控制列視窗註冊全域快捷鍵並處理 `WM_HOTKEY`，關閉時解除註冊；tooltip 與自動化名稱會標示快捷鍵（註冊失敗時省略）。
- `RecordingOverlayWindow`：Tooltip 開啟時取得其 Popup HWND 並排除錄影擷取，避免 tooltip 被錄進影片。
- `HotkeyGestureTests`：新增錄影快捷鍵測試。
- 版本提升至 1.22.0，同步更新 README、USER_GUIDE、CHANGELOG、RELEASE_NOTES。

### 問題與解法

- 開始通知原本在擷取開始後顯示，全螢幕錄影會把它錄進影片；改為全螢幕不顯示通知。
- 控制列是非啟動視窗，無法依靠鍵盤焦點；改在控制列 HWND 上註冊全域快捷鍵，生命週期與控制列一致。
- WPF Tooltip 是獨立 Popup 視窗，原本只排除控制列主視窗，造成 tooltip 被錄下；改為 tooltip 開啟後取得 Popup HWND 並套用 `ExcludeFromCapture`。
- x64 Debug 建置 0 警告、104 項測試與格式檢查通過。

### 待辦

- 尚未在實機錄影後檢視影片開頭、快捷鍵操作與 tooltip 排除結果。

### 日期與版本

2026-11-04，v1.22.0。

---

## 2026-11-04 - v1.21.0

### 目標

讓擷圖後開啟的編輯器預設以 100% 置中顯示，並記住上次編輯器視窗大小與最大化狀態。

### 變更

- `AppSettings`：新增 `EditorWindowSettings`（寬、高、是否最大化）與深拷貝。
- `JsonSettingsStore`：舊設定檔缺少編輯器視窗欄位時補預設值。
- `AppState`：新增 `RememberEditorWindow` 儲存編輯器視窗狀態。
- `EditorWindow`：首次顯示改以 100% 置中；建構時還原尺寸，`Loaded` 時套用最大化；視窗狀態變更、關閉與 App 結束時保存目前大小或還原尺寸。
- `App`：從系統匣結束前，先保存所有開啟中的編輯器視窗狀態。
- `AppSettingsTests`：新增預設值與深拷貝測試。
- 版本提升至 1.21.0，同步更新 README、USER_GUIDE、CHANGELOG、RELEASE_NOTES。

### 問題與解法

- 原本首次顯示會自動縮小以符合視窗；改為只設定一次 100% 置中，「符合視窗」與 `Ctrl+0` 仍保留。
- 最大化時需保存還原後尺寸，使用 `RestoreBounds`，避免下次開啟最大化後無法回到原本大小。
- 原先只在 `Closed` 儲存，但 `ShutdownMode=OnExplicitShutdown` 下從系統匣結束 App 不保證觸發視窗 `Closed`；改為狀態變更立即保存，並在 App 結束前主動保存所有編輯器。
- 關閉執行中的舊版 Clippings 後完成 x64 Debug 完整建置；0 警告、103 項測試與格式檢查通過。

### 待辦

- 尚未在實際多螢幕／混合 DPI 環境驗證視窗尺寸還原。

### 日期與版本

2026-11-04，v1.21.0。

---

## 2026-11-04 - v1.20.6

### 目標

調整設定頁區塊順序，讓常用外觀設定更靠前，螢幕錄影設定集中於頁面最下方。

### 變更

- `SettingsPage.xaml`：外觀卡片移至開機自動啟動後方，成為第二個區塊；螢幕錄影卡片移至所有設定區塊最後。
- `Directory.Build.props`、發行文件：版本提升至 1.20.6。

### 問題與解法

- 僅移動完整 XAML 卡片，不更動控制項名稱、事件或資料載入與儲存邏輯。
- x64 與 ARM64 Debug 建置 0 警告、101 項測試與格式檢查通過。
- x64 與 ARM64 framework-dependent Release 發佈及 ZIP 打包成功，ZIP 均排除 PDB；x64 發佈版啟動 smoke test 通過。

### 待辦

- ARM64 發佈版已完成建置與封裝，尚未在 ARM64 實機執行。

### 日期與版本

2026-11-04，v1.20.6。

---

## 2026-11-04 - v1.20.5

### 目標

讓設定頁的螢幕錄影說明清楚區分視窗錄影與全螢幕錄影的限制。

### 變更

- `SettingsPage.xaml`：說明文字開頭加入「視窗」，並新增第二行「全螢幕錄影無此限制。」。
- `Directory.Build.props`、發行文件：版本提升至 1.20.5。

### 問題與解法

- 原說明未明確指出限制只適用於視窗錄影；改成兩行文字，直接標示全螢幕錄影不受此限制。
- x64 與 ARM64 Debug 建置 0 警告、101 項測試與格式檢查通過。

### 待辦

- 無。

### 日期與版本

2026-11-04，v1.20.5。

---

## 2026-11-04 - v1.20.4

### 目標

修正 v1.20.3 將 WGC frame 複製至 CPU buffer 後，錄影畫面上下顛倒的問題。

### 變更

- `WindowsGraphicsRecordingService.cs`：在 BGRA `VideoEncodingProperties` 設定 `MF_MT_DEFAULT_STRIDE`，以正 stride 明確標示 frame buffer 為 top-down 排列。
- `Directory.Build.props`、發行文件：版本提升至 1.20.4。

### 問題與解法

- Direct3D／`SoftwareBitmap` frame 在記憶體中為 top-down；未提供 stride 時，Media Foundation 以 system-memory RGB 的預設方向解讀，造成輸出上下顛倒。設定每列 `width * 4` bytes 的正 stride，避免逐幀翻轉與額外 CPU 複製。
- x64 與 ARM64 Debug 建置 0 警告、101 項測試與格式檢查通過；另以「上紅、下藍」測試視窗實際錄製 MP4，播放畫面取樣確認方向正確。

### 待辦

- 無。

### 日期與版本

2026-11-04，v1.20.4。

---

## 2026-11-04 - v1.20.3

### 目標

修正錄影畫面永久凍結、停止前後影音不順、系統音訊偏小，以及最小化被誤判為尺寸變更的問題。

### 變更

- `WindowsGraphicsRecordingService.cs`：不再保留多格 WGC frame 給 encoder；每個 output sample 先將最新 frame 複製到 CPU `SoftwareBitmap`／`IBuffer`，再立即釋放 WGC frame，移除 frame reference 計數與 4 格上限。
- `WindowsGraphicsRecordingService.cs`：新增停止 drain；停止後 video/audio 繼續產生到停止時間點，最後一個 sample 的 duration 截齊，最多等待 2 秒後強制結束。
- `WindowsGraphicsRecordingService.cs`：新增最小化輪詢與 `TargetMinimized` 停止原因；0×0 frame 不再被當成尺寸變更。
- `SystemAudioLoopbackCapture.cs`、`AudioGain.cs`：系統音訊加入可調增益與防溢位截斷，預設 200%（+6 dB）。
- `RecordingSettings`、`RecordingOptions`、`SettingsPage`：新增「系統音訊音量」選項（100／150／200／300%）。
- `AppStrings.cs`、`SettingsPage.xaml`、`RecordingTargetDialog.xaml`：更新錄影停止提示文字為「錄影開始後若調整所選視窗大小或將視窗最小化，錄影會自動停止。」。
- 測試與版本：新增 `AudioGainTests`，更新錄影設定預設值測試；版本提升至 1.20.3。

### 問題與解法

- 原 pipeline 把 WGC frame 保留到 encoder 的 `Processed` callback；encoder 延遲時 frame pool 被占滿，之後只能重複舊 frame，造成畫面永久停住但聲音繼續。改為 frame 複製後立即釋放，讓 frame pool 永遠可接收新畫面。
- 停止時原本立即中斷 media source，最後約 1 秒的影音可能被截掉；改為 drain 到停止時間點，並讓 video/audio 使用同一個停止時間。
- 最小化時 WGC 會送出 0×0 frame，舊程式誤判為 `TargetResized`；改為以 `IsIconic` 判斷並使用新的停止原因。
- x64 與 ARM64 Debug 建置 0 警告、101 項測試與格式檢查通過。

### 待辦

- 需以 5–10 分鐘、含系統音訊的錄影實測凍結與結尾同步；若 WGC 本身停止供格，仍需追加 frame pool/session 重建的復原機制。
- CPU frame 複製在 4K/60 的效能尚未實測；必要時改為 GPU texture copy 或限制錄影解析度。

### 日期與版本

2026-11-04，v1.20.3。

---

## 2026-11-04 - v1.20.2

### 目標

修正錄影期間畫面可能停止但聲音繼續的問題，並讓使用者在選窗前得知尺寸限制、可從錄影控制列快速找回目標視窗。

### 變更

- `RecordingTargetDialog.xaml`、`AppStrings.cs`：新增錄影開始後改變視窗大小會自動停止的提醒。
- `WindowsGraphicsRecordingService.cs`：WGC frame 持有上限設為 frame pool 容量減一；壓力過高時丟棄待處理 frame，保留 pool 可繼續接收畫面。
- `WindowsGraphicsRecordingService.cs`：影音 sample request 使用獨立 gate 排序，長等待每 500 ms 回報進度；視訊 PTS 改依 frame index／FPS 計算。
- `WindowsGraphicsRecordingService.cs`：異常結束時只立即釋放零引用 frame；仍由編碼器使用的 frame 等待 `Processed` callback 後安全回收。
- `RecordingOverlayWindow.xaml(.cs)`：只有點擊目標視窗名稱時才執行還原與置前，控制按鈕不受影響。
- `WindowHelper.cs`、`NativeMethods.cs`：置前前重新驗證 HWND／Process ID，並以 `ShowWindowAsync(SW_RESTORE)` 還原最小化視窗。
- 文件與版本更新至 1.20.2。

### 問題與解法

- 原管線會讓每個送入編碼器的 sample 持有 WGC frame，若多個 `Processed` callback 延後，5 格 frame pool 可能全部被占用。改為最多持有 4 格，讓 WGC 始終有空間產生新 frame。
- 原視訊 PTS 使用 sample request 當下的錄影時間，延遲時可能跳躍並與固定 duration 不一致；改為固定 frame index 時間軸，錄影時鐘只負責 pacing。
- 靜態視窗本來就可能不產生新 frame，因此不能用 frame 到達逾時判定凍結；改從資源持有上限處理根因。
- x64 與 ARM64 Debug 均為 0 警告建置成功，96 項測試及格式檢查通過。

### 待辦

- 需以 30／60 FPS、系統音訊開／關及高 GPU 負載進行 10 分鐘以上實機錄影，並用 `ffprobe` 檢查影音 PTS 與時長。
- Windows 仍可能依前景視窗規則拒絕置前；失敗不會中斷錄影。

### 日期與版本

2026-11-04，v1.20.2。

---

## 2026-11-04 - v1.20.1

### 目標

改善錄影目標選擇與入口順序，並避免開始錄影通知音被系統音訊一併錄下。

### 變更

- `RecordingTargetDialog.xaml`：將內容區標題上移 8 DIP。
- `NativeMethods`、`WindowHelper`、`RecordingCoordinator`：確認目標後以 `SetForegroundWindow` 嘗試將視窗帶到最前方；失敗時記錄警告但不中斷錄影。
- `INotificationService`、`TrayNotificationService`：新增通知聲音開關；「已開始錄影」保留畫面通知但不播放聲音。
- `MainWindow.xaml`、`App.xaml.cs`：主視窗與系統匣皆將「全螢幕錄影」移到「選擇視窗錄影」之前。
- 版本提升至 1.20.1。

### 問題與解法

- 錄影開始通知原本在 WASAPI loopback 啟動後播放，因此通知音會進入影片；改用 H.NotifyIcon 的 `sound: false`，不需延遲錄影開始。
- Windows 可能依前景視窗規則拒絕 `SetForegroundWindow`，因此置前採 best-effort，不讓焦點限制造成錄影失敗。
- 驗證期間執行中的 Clippings 鎖住標準 Debug 輸出，改用獨立暫存輸出路徑執行完整建置。
- x64 與 ARM64 Debug 隔離輸出皆為 0 警告建置成功，96 項測試及 `dotnet format --verify-no-changes` 通過。

### 待辦

- 需手動確認不同應用程式的前景視窗限制，以及開始通知在 Windows 10／11 上皆保持靜音。

### 日期與版本

2026-11-04，v1.20.1。

---

## 2026-11-04 - v1.20.0

### 目標

讓視窗與全螢幕錄影可選擇包含電腦播放的聲音，同時維持既有暫停、時間軸與 MP4 完檔行為。

### 變更

- `RecordingOptions`、`RecordingSettings`、`SettingsPage`：新增預設關閉的系統音訊選項。
- `SystemAudioLoopbackCapture`：使用 NAudio WASAPI loopback 擷取預設播放裝置，轉換為 48 kHz、雙聲道、16-bit PCM。
- `WindowsGraphicsRecordingService`：加入 PCM audio stream 與 192 kbps AAC 輸出，以 20 ms 封包和影片共用錄影時間軸。
- `RecordingControl`：新增暫停世代編號；音訊管線在暫停期間丟棄資料，恢復時清空舊緩衝。
- 測試與文件：補上設定預設值、暫停世代測試、使用指南與隱私說明；版本提升至 1.20.0。

### 問題與解法

- 執行中的 Clippings 鎖住 Debug 輸出 DLL，先以獨立暫存輸出確認完整方案可編譯，關閉程式後再執行標準建置。
- 播放裝置格式可能是 44.1／48／96 kHz 或浮點、多聲道，透過 NAudio Media Foundation resampler 統一為媒體來源的固定 PCM 格式。
- 方案未定義 `Debug|arm64` 組態，ARM64 改以 `Clippings.App.csproj -p:Platform=arm64` 驗證；x64、ARM64 Debug 均為 0 警告建置成功，96 項測試與格式檢查通過。

### 待辦

- 尚需在實際播放音訊下檢查 MP4 的 AAC stream、長時間影音漂移及播放裝置中途拔除行為。
- DRM、獨占模式、遠端桌面與特殊音效驅動可能無法提供 loopback 音訊。

### 日期與版本

2026-11-04，v1.20.0。

---

## 2026-11-03 - v1.19.0

### 目標

改善日常介面操作與可及性，讓清單、框選、編輯器、設定頁與系統匣都能用更少的步驟完成常用動作。

### 變更

- `HistoryPage`：新增 `Enter` 編輯、`Ctrl+C` 複製、`Delete` 刪除；卡片加入右鍵選單。
- `ClipboardPage`：新增「只看釘選」、雙擊／`Enter` 複製、`Ctrl+C`、`Delete`、`P` 釘選與卡片右鍵選單。
- `HistoryPage`、`ClipboardPage`：右鍵選單支援在檔案總管中顯示與複製檔案路徑；卡片按鈕補上 ToolTip／AutomationProperties。
- `MainWindow`：快速擷取按鈕的 ToolTip 顯示目前快捷鍵。
- `SettingsPage`：新增 `Ctrl+S`、錄影／存檔／歷史庫資料夾的「開啟」按鈕。
- `EditorWindow`：新增 `Ctrl++`／`Ctrl+-` 縮放與 `Ctrl+0` 符合視窗。
- `CaptureOverlayWindow`：`Shift` 拖曳固定正方形，選取後可用方向鍵微調。
- `App`：系統匣新增「啟用剪貼簿歷史」與「開啟 Clippings 資料夾」。
- `WindowSizingHelper`：主視窗與編輯器依 `SystemParameters.WorkArea` 調整初始尺寸。
- 文件：更新 `USER_GUIDE`、`TECHNICAL`、`CHANGELOG`、`RELEASE_NOTES`；版本提升至 1.19.0。

### 問題與解法

- 歷史頁使用 `WrapPanel` 無法真正虛擬化，因此維持增量載入；本次只補上鍵盤與右鍵操作。
- 右鍵選單的 `DataContext` 不會自動從卡片繼承，改用 `PlacementTarget.DataContext` 綁定。
- 設定頁的 `Ctrl+S` 會先排除快捷鍵輸入框，避免錄製 `Ctrl+S` 時被儲存動作攔截。

### 待辦

- 尚未手動驗證多螢幕 DPI 下的視窗尺寸調整與框選方向鍵微調。
- 尚未實測右鍵選單在深色主題與鍵盤操作下的焦點行為。

### 日期與版本

2026-11-03，v1.19.0。

---

## 2026-11-03 - v1.18.1

### 目標

執行第一波低風險、高效益的效能與穩定性優化，重點放在擷取等待路徑、日誌／資料庫寫入，以及大量歷史項目的 UI 負擔。

### 變更

- `CaptureCoordinator.cs`：只有主視窗可見或螢幕標註進行中時才隱藏並等待畫面穩定；主視窗未顯示時直接擷取。
- `CaptureCoordinator.cs`：擷取完成後先複製到剪貼簿，再編碼 PNG、寫入歷史與執行保留策略。
- `App.xaml.cs`：新增 `Microsoft.EntityFrameworkCore.Database.Command` 的 `Warning` 過濾，避免每個查詢都寫入檔案日誌。
- `FileLoggerProvider.cs`：改用容量 2048 的有界 Channel（`DropOldest`），以批次方式透過單一 `StreamWriter` 寫入並定時 flush。
- `ClipboardWatcher.cs`：先對原始像素計算 SHA-256，重複內容直接略過，不再重複編碼 PNG／縮圖；雜湊狀態加上鎖定。
- `ClippingsStore.cs`、`SqlitePragmaInterceptor.cs`：連線字串加入 `Pooling=True`，連線開啟時套用 `synchronous=NORMAL`、`temp_store=MEMORY`、`cache_size=-8192`。
- `HistoryPage.xaml(.cs)`：改為每頁 60 筆的增量載入，避免一次建立全部卡片。
- `ClipboardPage.xaml`：`CanContentScroll` 改為 `True`，讓預設 `VirtualizingStackPanel` 正常虛擬化。
- `PathToImageConverter.cs`：以檔案路徑、解碼寬度與 `LastWriteTimeUtc` 做縮圖快取，避免切頁時重複解碼。
- 版本提升至 1.18.1。

### 問題與解法

- `HistoryPage` 使用 `WrapPanel`，即使開啟虛擬化也無法真正回收容器；因此改採增量載入，保留現有卡片排版並避免新增套件。
- `ClipboardPage` 原本 `CanContentScroll="False"` 會讓虛擬化失效，改回 `True` 即可沿用預設虛擬化面板。
- `synchronous=NORMAL` 在 WAL 模式下仍維持資料庫一致性，但極端斷電時可能少最後一筆交易；已記錄為可接受取捨。

### 待辦

- 尚未實測大量歷史（500～1000 筆）下的增量載入手感與捲動位置。
- 尚未實測 `FileLoggerProvider` 跨日換檔與磁碟寫入失敗後的重開行為。
- 尚未比較 SQLite PRAGMA 調整前後的實際寫入延遲。

### 日期與版本

2026-11-03，v1.18.1。

---

## 2026-11-03 - v1.18.0

### 目標

新增全螢幕錄影與錄影中浮動控制列，支援暫停／繼續，並修正倒數視窗取消按鈕被裁切的問題。

### 變更

- `Platform/Interop/RecordingTarget.cs`、`MonitorInfo`、`MonitorService`：新增視窗／螢幕共用錄影目標與 HMONITOR。
- `WindowsGraphicsCaptureService`、`WindowsGraphicsRecordingService`：新增 `CreateForMonitor` 與 `RecordMonitorAsync`，與視窗錄影共用管線。
- `Core/Models/RecordingControl.cs`、`RecordingState.Paused`：新增可暫停計時器與 sample request 暫停 gate。
- `RecordingOverlayWindow.xaml(.cs)`、`RecordingOverlayService.cs`：目標螢幕右上角控制列，含紅色指示、時間、名稱、暫停／繼續／停止。
- `RecordingCoordinator.cs`：整合視窗／全螢幕目標、暫停／繼續、控制列生命週期與主視窗隱藏／還原。
- 主視窗與系統匣新增「全螢幕錄影」，錄影卡片與設定頁改為「螢幕錄影」。
- `RecordingCountdownWindow` 改為 `SizeToContent` 並在版面完成後定位。
- 測試與文件：新增 `RecordingControl` 測試與 `Paused` 列舉穩定值；版本提升至 1.18.0。

### 問題與解法

- `PixelRect` 沒有 `Top` 屬性，控制列定位改用 `Y`。
- 控制列 `SizeToContent` 後寬度會隨狀態文字變動，加入 `SizeChanged` 重新定位。
- 暫停時若直接停止，等待中的 sample request 可能卡住 EOS；`SignalEndOfStream` 會呼叫 `ReleaseWaiters()`。
- 以暫存 console 實測全螢幕錄影：1.5 秒錄影、暫停 2 秒、再錄 1.5 秒，`ffprobe` 顯示 H.264、1920×1080、30 FPS、2.5333 秒，確認暫停時間已排除。

### 待辦

- 尚未手動驗證長時間暫停（建議 30～60 秒）是否會觸發 MediaStreamSource 逾時。
- 尚未實測混合 DPI、多螢幕下的控制列定位與全螢幕錄影。
- 音訊、區域錄影與影片編輯尚未實作。

### 日期與版本

2026-11-03，v1.18.0。

---

## 2026-11-03 - v1.17.0

### 目標

讓每次錄影都能明確選擇目標視窗，並提供不計入影片時間的延遲倒數，避免切換視窗與編碼器準備時間被錄入。

### 變更

- `Platform/Interop/WindowEnumerationService.cs`、`App/Views/RecordingTargetDialog.xaml(.cs)`：列出目前可見、未最小化且可擷取的外部視窗，支援重新整理、雙擊與鍵盤確認。
- `Core/Models/RecordingOptions.cs`、`SettingsPage.xaml(.cs)`：新增可持久化的 0／5／10／20／30 秒錄影開始延遲。
- `RecordingCountdownWindow.xaml(.cs)`、`RecordingCountdownService.cs`：新增置中於目標、不搶焦點且排除擷取的浮動倒數，可直接取消。
- `WindowsGraphicsRecordingService.cs`：Transcoder 準備完成後才執行倒數，完成並重新驗證目標後才啟動 capture session。
- `RecordingCoordinator.cs`、主視窗與系統匣：共用選窗流程，新增 `Countdown` 狀態、剩餘秒數及取消控制；錄影計時從實際開始擷取後才啟動。
- 測試與文件：補上延遲預設值、深層複製與列舉穩定值測試；版本提升至 1.17.0。

### 問題與解法

- 系統匣初次接線仍引用舊錄影 API，建置出現 CS1061；改為新的共用選窗入口與 `CanStartRecording` 狀態。
- 為避免編碼器初始化時間縮短使用者設定的延遲，倒數回呼放在 `PrepareMediaStreamSourceTranscodeAsync` 完成後、`StartCapture()` 前。
- 同時執行 WPF 建置與 `dotnet format` 會競爭 `obj` 中的 BAML 暫存檔並產生 BG1002；改為依序驗證後建置成功。

### 待辦

- 尚未手動驗證混合 DPI、多螢幕下的倒數置中與實際 WGC 錄影；自動建置與 91 項測試已通過。
- 音訊、暫停、螢幕／區域錄影與動態尺寸縮放尚未實作。

### 日期與版本

2026-11-03，v1.17.0。

---

## 2026-11-03 - v1.16.0

### 目標

讓第一版視窗錄影可從主視窗直接操作、提供常用品質與輸出設定，並補上磁碟空間、暫存檔與完成檔案操作等實際使用所需的穩定性保護。

### 變更

- `Core/Models/RecordingOptions.cs`、`AppSettings.cs`：新增可持久化的 `RecordingSettings`（輸出資料夾、30／60 FPS、4／8／12／20 Mbps、游標）並深層複製。
- `Platform/Capture/WindowsGraphicsRecordingService.cs`：套用幀率、位元率與游標設定；錄影中每 5 秒檢查磁碟空間，低於 64 MB 時停止。
- `Platform/Storage/JsonSettingsStore.cs`：載入時修復為 `null` 的 `Annotation`／`Recording` 子設定。
- `App/Services/RecordingCoordinator.cs`：讀取設定快照、開始前檢查 256 MB、清除殘留暫存檔、記錄開始時間與上次輸出，提供開啟影片／資料夾。
- `App/Views/MainWindow.xaml(.cs)`：新增錄影卡片、準備／錄影／完檔狀態、每秒計時與完成檔案操作。
- `App/Views/SettingsPage.xaml(.cs)`：新增錄影設定卡片並納入未儲存變更 snapshot。
- `App/App.xaml.cs`：系統匣新增開啟上次錄影／資料夾，錄影期間更新 ToolTip。
- 測試與文件：新增錄影設定預設值與深層複製測試，更新 README、使用指南、架構、技術、系統設計、隱私、變更紀錄與版本說明；版本提升至 1.16.0。

### 問題與解法

- 執行中的 Clippings 鎖住 Debug 輸出 DLL，標準 `dotnet build` 無法覆寫；驗證改用 `-p:OutputPath` 指向核准 Temp 路徑，不中斷正在執行的使用者實例。
- 直接共用 `BaseIntermediateOutputPath` 會讓多專案產生同名 AssemblyInfo 而重複；改為只覆寫 App build 的 `OutputPath`，保留各專案原本 obj 隔離。
- 60 FPS／12 Mbps／不含游標 smoke test 以 `ffprobe` 驗證：H.264、982×740、60 FPS、179 frames、2.983317 秒。

### 待辦

- 尚未完成視窗改變尺寸時的 GPU scaling／letterbox；目前仍會自動停止。
- 尚未實測 30 分鐘、4K、ARM64、多 GPU、HDR、遠端桌面與 Windows N。
- 音訊、暫停、螢幕／區域錄影與影片編輯尚未實作。

### 日期與版本

2026-11-03，v1.16.0。

---

## 2026-11-03 - v1.15.0

### 目標

完成第一版目前視窗錄影：從系統匣開始／停止，使用 Windows.Graphics.Capture 持續取得 frame，輸出可正常完檔的無聲 H.264 MP4，並確保靜態畫面、編碼落後與程式結束時不造成時長錯誤或損壞檔案。

### 變更

- `Core/Models/RecordingOptions.cs`：新增錄影選項、狀態、停止原因與結果模型；錄影與圖片 `CaptureMode` 分離。
- `Core/Helpers/PathUtil.cs`：新增預設 `影片\Clippings` 路徑與錄影檔名。
- `Platform/Capture/WindowsGraphicsRecordingService.cs`：新增 WGC 5-buffer frame pool、容量 1 latest-frame slot、Direct3D surface 引用計數、MediaStreamSource、MediaTranscoder、H.264 MP4、EOS 完檔與暫存檔搬移。
- `Platform/Capture/WindowsGraphicsCaptureService.cs`：共用 D3D／GraphicsCaptureItem 建立流程，並修正 `IDXGIDevice` IID。
- `App/Services/RecordingCoordinator.cs`：管理 `Idle／Starting／Recording／Stopping`、目標視窗、輸出路徑、停止、通知與錯誤。
- `App/App.xaml.cs`：系統匣新增開始／停止錄影，依狀態動態切換；程式結束時最多等待 15 秒完成錄影。
- `App/Strings/AppStrings.cs`：新增錄影操作與通知文字。
- `tests/Clippings.Core.Tests/AppSettingsTests.cs`：新增錄影檔名與狀態列舉穩定性測試。
- 文件與版本：更新 README、使用指南、架構、技術、系統設計、隱私、變更紀錄與版本說明；版本提升至 1.15.0。

### 問題與解法

- `IDXGIDevice` IID 原字串少一段十六進位數字，建置成功但執行期 `Guid` 初始化拋出 `FormatException`，導致 v1.14.0 WGC 一律退回 GDI；改為正確 IID `54EC77FA-1377-44E6-8C32-88FD5F44C84C`。
- 靜態視窗不會持續觸發 WGC `FrameArrived`，最初輸出只有一個 frame／0.033 秒；改為保留目前 GPU surface 並按單調時鐘重複提交樣本。
- 以 `MediaStreamSample.Processed` 作為單一 semaphore 會與編碼器下一個 SampleRequested 循環等待；改用 frame 引用計數，允許同一 surface 有多個在途樣本，取代且引用歸零後才釋放。
- 測試計時器若在 MediaTranscoder 初始化前啟動，會把準備時間算入錄影；新增 `recordingStarted` 回呼，成功 StartCapture 後才切換狀態與通知。
- smoke test 以 `ffprobe` 驗證 3 秒靜態視窗：H.264、982×740、30 FPS、90 frames、3.000000 秒，MP4 可正常解析。

### 待辦

- 尚未實測長時間、4K、ARM64、多 GPU、HDR、遠端桌面、Windows N 與磁碟空間不足。
- 第一版只有單一視窗無聲錄影；不支援螢幕／區域、系統音訊、麥克風、暫停、動態縮放或影片編輯。
- 目標視窗改變尺寸時會自動停止，尚未加入固定畫布縮放或 letterbox。

### 日期與版本

2026-11-03，v1.15.0。

---

## 2026-11-03 - v1.14.0

### 目標

為後續螢幕錄影先驗證 Windows.Graphics.Capture 管線，並直接改善目前視窗被其他視窗遮擋時的擷取結果，同時維持 Windows 10 1809 相容性。

### 變更

- `Platform/Capture/WindowsGraphicsCaptureService.cs`：新增 HWND 建立 `GraphicsCaptureItem`、Direct3D 11 BGRA 裝置、free-threaded frame pool、首幀 `SoftwareBitmap` 複製與 5 秒逾時。
- `App/Services/CaptureCoordinator.cs`：目前視窗在 Windows 10 1903 以上優先使用 Windows.Graphics.Capture；不支援、初始化失敗、逾時或 GPU 擷取失敗時記錄警告並退回既有 GDI 可見畫面擷取。
- `App/App.xaml.cs`：將新擷取服務註冊至 DI。
- 文件與版本：同步更新使用指南、技術與架構文件、變更紀錄及版本說明；版本提升至 1.14.0。

### 問題與解法

- 專案最低版本是 Windows 10 1809，但從 HWND 建立擷取項目需要 Windows 10 1903；以 OS build 與 `GraphicsCaptureSession.IsSupported()` 雙重能力檢查處理，不提高最低版本。
- WGC 需要 Direct3D 11 與 CsWinRT COM interop；目前目標框架已提供 Windows SDK 投影，不需新增 NuGet 套件。
- 平台擷取可能因 GPU、遠端桌面或受保護內容失敗；保留 GDI fallback，避免既有功能退步。

### 待辦

- 尚未在實體 Windows 10 1903、Windows 11、ARM64、混合 DPI、多 GPU 與遠端桌面環境手動驗證。
- Windows.Graphics.Capture 目前只取單張視窗首幀，尚未延伸到螢幕錄影、音訊或持續 frame pipeline。

### 日期與版本

2026-11-03，v1.14.0。

---

## 2026-11-03 - v1.13.1

### 目標

1. 調整編輯器右側按鈕順序與語意：最近擷取為「複製、另存新檔、儲存、關閉」；新擷取為「複製、另存新檔、儲存、捨棄」。
2. 「儲存」寫入目前編輯結果後關閉；「關閉」直接關閉、不儲存編輯；「捨棄」刪除該筆新擷取後直接關閉。
3. 取消關閉時自動存回，改為只有按下「儲存」才寫入最近擷取。

### 變更

- `App/Views/EditorWindow.xaml`：`SaveButton` 更名為 `SaveAsButton`；新增 `SaveLibraryButton`（「儲存」）；`DiscardButton` 與 `CloseButton` 依開啟來源擇一顯示。
- `App/Views/EditorWindow.xaml.cs`：新增 `OnSaveLibraryClick` 與 `SaveToLibraryAndCloseAsync()`，寫入 PNG／縮圖後關閉；`OnDiscardClick` 移除二次確認，直接刪除新擷取並關閉；`OnWindowClosing` 移除自動存回與 `_closePending`／`_discarding`；`SetBusy` 同步停用儲存、另存新檔與捨棄。
- 鍵盤：`Ctrl+S` 改為「儲存」；`Ctrl+Shift+S` 為「另存新檔」。
- `App/Strings/AppStrings.cs`：調整「另存新檔」、「儲存」、「關閉」、「捨棄」ToolTip；移除捨棄確認文字；`FormatAutoSaveToLibraryFailed` 更名為 `FormatSaveToLibraryFailed`。
- 文件與版本：更新 `CHANGELOG.md`、`RELEASE_NOTES.md`、`README.md`、`docs/USER_GUIDE.md`、`docs/TECHNICAL.md`、`docs/SYSTEM_DESIGN.md`；版本提升至 1.13.1（`AssemblyVersion` 維持 1.13.0.0）。

### 問題與解法

- 原本關閉時自動存回與新的「關閉／捨棄」語意衝突，改為僅由「儲存」按鈕寫入；視窗 X 與「關閉」都不儲存。
- 新擷取在開啟編輯器前已寫入歷史庫，因此「捨棄」仍刪除該筆紀錄與圖檔；按 X 只關閉，保留擷取當下的原始圖片。
- 既有項目在沒有編輯時按「儲存」直接關閉，避免無意義地更新時間與排序；若來源項目尚未寫入（`Id == 0`）仍會嘗試新增一筆。
- 儲存是非同步流程，忙碌中關閉會被取消，避免寫入中斷。

### 待辦

- 尚未實測儲存失敗後改按關閉或捨棄的流程。
- 尚未實測文字輸入焦點下 `Ctrl+Shift+S` 的行為。

### 發布

- 建立 GitHub 公開儲存庫 `mark216tw/win-clippings-01`，授權為 MIT License。
- `Directory.Build.props` 新增 arm64 平台支援，`PlatformTarget` 依 `Platform` 決定。
- GitHub Release v1.13.1 僅提供非 self-contained（framework-dependent）版本：x64 與 ARM64 各一個 ZIP。
- 同步更新 `README.md`、`RELEASE_NOTES.md`、`docs/BUILD_AND_RELEASE.md` 的下載與發佈說明。

### 日期與版本

2026-11-03，v1.13.1。

---

## 2026-11-03 - v1.13.0

### 目標

1. 新擷取開啟編輯器時提供「捨棄」功能：按下後關閉編輯器，且不保留這張擷圖；從最近擷取開啟編輯器時不提供。
2. 最近擷取新增「刪除全部」，只刪除未收藏的擷取；收藏的圖片保留，由使用者自行手動刪除。
3. 編輯器右上角動作區最右側新增「關閉」按鈕，功能與視窗右上角 X 相同。

### 變更

- `Core/Services/IClippingsStore.cs`、`Platform/Storage/ClippingsStore.cs`：新增 `DeleteNonFavoriteCapturesAsync`，刪除 `IsFavorite == false` 的資料列與圖檔並回傳筆數，收藏項目不受影響。
- `App/Views/EditorWindow.xaml`：右側動作區新增「捨棄」按鈕（預設隱藏）與最右側的「關閉」按鈕。
- `App/Views/EditorWindow.xaml.cs`：建構子新增 `canDiscard` 參數；新增捨棄流程（二次確認、刪除 `_origin` 紀錄、通知歷史頁重載、以 `_discarding` 跳過關閉時自動存回）；`SetBusy` 一併停用捨棄按鈕；新增 `OnCloseClick` 直接呼叫 `Close()`，沿用與視窗 X 相同的關閉流程。
- `App/Services/CaptureCoordinator.cs`：新擷取開啟編輯器時傳入 `canDiscard: true`。
- `App/Views/HistoryPage.xaml`、`HistoryPage.xaml.cs`：新增「刪除全部」按鈕與 `OnDeleteAllClick`（二次確認、刪除未收藏、重載列表、顯示刪除筆數）；從歷史開啟編輯器改傳 `canDiscard: false`，不顯示捨棄按鈕。
- `App/Views/MainWindow.xaml.cs`：新增 `NotifyCaptureDeleted()`，供捨棄後刷新最近擷取頁。
- `App/Strings/AppStrings.cs`：新增捨棄、關閉與刪除全部的提示文字、確認訊息與結果格式化方法。
- 文件與版本：`CHANGELOG.md`、`RELEASE_NOTES.md`、`README.md`、`docs/USER_GUIDE.md` 補充本次功能；版本提升至 1.13.0。

### 問題與解法

- 編輯器開啟前擷取就已寫入歷史庫（`CaptureCoordinator` 先呼叫 `AddCaptureAsync`），因此「捨棄」不是單純關閉，而是刪除剛寫入的紀錄與圖檔；必須等刪除完成再關閉，避免觸發 `Closing` 的自動存回。
- 從最近擷取開啟的項目也有 `Id`，無法用 `Id` 判斷來源，改由呼叫端明確傳入 `canDiscard`。
- 「刪除全部」在搜尋或「只看收藏」狀態下仍要刪除全部未收藏，因此按鈕不受篩選影響，實際刪除以資料庫 `IsFavorite == false` 為準。
- 捨棄不影響擷取流程早於編輯器完成的自動複製剪貼簿或自動存檔輸出。

### 待辦

- 尚未實測大量擷取（數千筆）時「刪除全部」的耗時與 UI 反應。
- 尚未實測歷史頁正在顯示時按下捨棄，清單是否立即更新。

### 日期與版本

2026-11-03，v1.13.0。

---

## 2026-11-02 - v1.12.0

### 目標

同日兩批工作：

1. 剪貼簿歷史若是圖片，提供「載入到最近擷取」的功能：把剪貼簿中的圖片轉成擷取項目存入最近擷取歷史庫，方便與螢幕擷取圖片統一管理。
2. 剪貼簿歷史調整為只顯示圖片：取消類型切換與搜尋、文字／檔案不再記錄、圖片卡片文字改「（圖片）」、空歷史隱藏「清除未釘選」、新增「重新整理」；編輯器每次開啟固定以「平移」起始（不再記住上次使用的工具）。
3. 設定頁「最多保留」（剪貼簿歷史、歷史庫）新增 50 選項。
4. 編輯器：標註／裁切後關閉自動把編輯結果存回最近擷取（不詢問未儲存）；「儲存」按鈕更名「另存新檔」。實作後使用者更正：應**覆蓋原本的擷取項目**而非新增一筆，同日修正。

### 變更

一、加入擷取：

- `App/Views/ClipboardPage.xaml`：卡片按鈕區新增 `AddToCaptureButton`（文字「加入擷取」、ToolTip「載入到最近擷取」，含 `AutomationProperties.Name`）
- `App/Views/ClipboardPage.xaml.cs`：新增 `OnAddToCaptureClick`——背景執行緒以 `ImageCodec.Load` 載入 `ClipboardItem.FilePath`，編碼 PNG（`ImageCodec.Encode`）與縮圖（`ImageCodec.EncodeThumbnailJpeg`），建立 `CaptureItem`（`Source = "剪貼簿匯入"`）後呼叫 `IClippingsStore.AddCaptureAsync` 寫入歷史庫，成功後以 `MainWindow.NavigateTo(0)` 自動切到最近擷取頁
- `App/Strings/AppStrings.cs`：新增 `ActionAddToCapture`、`ActionAddToCaptureToolTip`、`CaptureSourceFromClipboard`、`AddToCaptureMissingFile`、`FormatAddToCaptureFailed`

二、只顯示圖片與相關調整：

- `App/Views/ClipboardPage.xaml`：移除搜尋列與「顯示」類型切換列；清單模板固定呈現圖片（縮圖＋固定文字「（圖片）」，原「（影像）」DataTrigger 一併移除）；右上改為「重新整理」「清除未釘選」兩顆按鈕
- `App/Views/ClipboardPage.xaml.cs`：`ReloadAsync` 固定查詢 `ClipboardItemKind.Image`、空狀態固定 `EmptyClipboardImages`，並依有無項目切換 `ClearButton` 顯示（空歷史隱藏「清除未釘選」）；新增 `OnRefreshClick`（對齊 `HistoryPage` 模式）；移除搜尋／篩選相關欄位與處理常式、`FocusSearch`；`OnCopyClick` 簡化為圖片複製
- `App/Views/MainWindow.xaml.cs`：`Ctrl+F` 只在最近擷取頁聚焦搜尋（剪貼簿頁已無搜尋框）
- `App/Services/ClipboardWatcher.cs`：只擷取圖片剪貼簿（移除檔案／文字分支）；`ClipboardSnapshot` 精簡為 `(ImageBytes, ThumbnailBytes, Hash)`
- `App/App.xaml.cs`：`OnClipboardCaptured` 固定寫入 `Kind = Image`、不再寫 `TextContent`
- `App/Helpers/ClipboardHelper.cs`：移除未引用的 `TrySetText`、`TrySetFiles`、`TryGetText`、`TryGetFiles`
- `App/Strings/AppStrings.cs`：移除未引用的 `EmptyClipboardHistory`、`EmptyClipboardTexts`、`EmptyClipboardFiles`；新增 `CopyImageMissingFile`
- 編輯器固定平移：`Core/Models/AppSettings.cs` 移除 `LastTool`；`App/Services/AppState.cs` 移除 `RememberLastTool`；`App/Views/EditorWindow.xaml.cs` 建構子固定 `AnnotationTool.Pan`、移除三處 `RememberLastTool` 呼叫與啟動時橡皮擦還原；`tests/Clippings.Core.Tests/AppSettingsTests.cs` 移除兩個 `LastTool` 測試

三、設定頁最多保留新增 50：

- `App/Views/SettingsPage.xaml.cs`：`MaxClipboardCombo`／`MaxCaptureCombo` 的 `ItemsSource` 改為 `{ 50, 100, 200, 500, 1000 }`，`LoadSettings` 合法值檢查同步加 `or 50`（否則已儲存的 50 會被重置回 200）
- `App/Views/SettingsPage.xaml`：剪貼簿歷史說明文字改「啟用剪貼簿歷史（監聽系統複製的圖片）」（上輪「只記錄圖片」的遺漏修正）

四、編輯器關閉自動存回最近擷取（覆蓋原本項目）、「另存新檔」更名：

- `Core/Services/IClippingsStore.cs`、`Platform/Storage/ClippingsStore.cs`：新增 `UpdateCaptureImageAsync(id, imageBytes, extension, thumbnailBytes, width, height)`——原地覆寫既有 `FilePath`／`ThumbnailPath`（路徑為空或檔案已不存在才以 `PathUtil.BuildImageFileName` 產生新檔名），更新 `Hash`／`Width`／`Height`／`CreatedAt`（編輯完成時間，使用者決定），`Source`／`IsFavorite` 不動；找不到該 `id` 回傳 `null`
- `App/Views/EditorWindow.xaml.cs`：`OnWindowClosing` 移除「這張圖片還沒儲存，確定要關閉嗎？」MessageBox，改為 `IsDirty` 時取消關閉並呼叫 `SaveToLibraryThenCloseAsync()`（`_closePending` 防重入）；該方法背景合成＋編碼 PNG 與縮圖（無註解直接編碼 `Bitmap`，有註解用 `RenderComposite(SKColors.Transparent)` 保留透明度），優先以 `_origin.Id` 呼叫 `UpdateCaptureImageAsync` **覆蓋原本項目**；原項目已不存在（回傳 `null`）或 `_origin.Id == 0`（來源擷取未寫入成功）→ 自動改為新增一筆（`Source` 沿用原始項目）＋`TrimCapturesAsync`；成功後清 `IsDirty`、`MainWindow.NotifyCaptureStored()`、再 `Close()`；失敗記錄＋MessageBox＋狀態列後留在編輯器（再次關閉即重試）
- `App/Views/EditorWindow.xaml.cs`：`SaveToFileAsync` 移除 `IsDirty = false`——「另存新檔」僅匯出檔案，只要編輯過，關閉一律自動存回（依使用者決定）
- `App/Views/EditorWindow.xaml`：`SaveButton` 內容改 `{x:Static AppStrings.ActionSaveAs}`（「另存新檔」）、ToolTip「另存新檔為圖檔 (Ctrl+S)」；`Ctrl+S` 行為不變
- `App/Views/EditorWindow.xaml.cs`：建構子改為 `EditorWindow(SKBitmap, AppState, IClippingsStore, ILoggerFactory, CaptureItem? origin)`（自動存回需要 store、logger 與原始項目）
- `App/Views/HistoryPage.xaml.cs`：注入 `ILoggerFactory`，`OpenEditorAsync` 傳入 `_store` 與 `item`
- `App/Services/CaptureCoordinator.cs`：注入 `ILoggerFactory`，`OpenEditor(SKBitmap, CaptureItem origin)` 由 `HandleCaptureAsync` 傳入剛建立的 `item`（含 `Id`）
- `App/Strings/AppStrings.cs`：新增 `ActionSaveAs`、`SaveAsToolTip`、`FormatAutoSaveToLibraryFailed`
- `App/Helpers/PathToImageConverter.cs`：加 `BitmapCreateOptions.IgnoreImageCache`——修復覆蓋後清單仍顯示舊縮圖（WPF URI 影像快取，見下方問題與解法）

五、文件同步與發佈：

- 文件同步：`docs/USER_GUIDE.md`（剪貼簿只記錄圖片、加入擷取／重新整理、編輯器固定平移／另存新檔／關閉自動存回、設定選項 50）、`docs/TECHNICAL.md`（移除 `LastTool` 說明、`UpdateCaptureImageAsync`、剪貼簿監聽只記錄圖片、編輯器關閉覆蓋流程）、`docs/ARCHITECTURE.md`、`docs/SYSTEM_DESIGN.md`（UC-05／UC-07／FR-09 更新、新增 FR-18）、`docs/PRIVACY.md`（僅記錄圖片）、`docs/BUILD_AND_RELEASE.md`（版本範例更新）
- 發佈：GitHub Release v1.12.0（完整免安裝版 `Clippings-1.12.0-win-x64.zip`、精簡版 `Clippings-1.12.0-win-x64-framework-dependent.zip`），提交者 `mark216tw <mark216tw@gmail.com>`
- 文件更新：`CHANGELOG.md`、`README.md`、`RELEASE_NOTES.md`
- 版本維持 1.12.0（同日工作併入，未另升版）
- 文件更新：`CHANGELOG.md`、`README.md`、`RELEASE_NOTES.md`

### 問題與解法

- 圖片檔可能已被清理（`clipboard\` 下 PNG 遺失）：點擊前先 `File.Exists` 檢查，缺失時留在本頁顯示對應訊息（`AddToCaptureMissingFile`／`CopyImageMissingFile`），不切頁
- 重複圖片處理：擷取資料表不做 hash 去重，依使用者決定一律新增
- `CaptureItem.Hash` 留空，由 `ClippingsStore.AddCaptureAsync` 自動以 `HashUtil.Sha256Hex` 計算
- 移除 `LastTool` 還原後 `EditorWindow._state` 變成已賦值但未使用的欄位（`TreatWarningsAsErrors` 會失敗）→ 一併移除 `_state` 欄位，建構子仍由參數讀取 `state.Settings`
- `ClipboardWatcher` 移除 `using Clippings.Core.Models` 後 `ImageFormat` 編譯失敗（CS0103）→ 該 using 為 `ImageCodec.Encode(bitmap, ImageFormat.Png)` 所需，予以保留
- 舊的文字／檔案剪貼簿紀錄仍留在 SQLite，查詢固定 `Kind = Image` 永不顯示；未做資料移除
- 「最多保留」選項清單在 code-behind 的 `ItemsSource` 與 `LoadSettings` 合法值檢查兩處維護，加選項時必須同步，避免存了值卻被重置
- 關閉（`Closing`）是同步事件，自動存入需非同步 → 取消關閉、存入成功後再 `Close()`（先清 `IsDirty` 讓重入的 `Closing` 放行）；`_busy` 期間仍取消關閉
- 合成背景選 `SKColors.Transparent` 保留圖片透明度（螢幕擷取為不透明，僅含 alpha 的剪貼簿圖片受影響）；「另存新檔」既有行為（註解合成白底）不變
- 覆蓋語意（使用者更正）：初版關閉時新增一筆，改為 `UpdateCaptureImageAsync` 覆蓋原始項目；原地覆寫同一 `FilePath`／`ThumbnailPath`；原檔名內含舊 hash8 僅為命名慣例，不影響功能
- **實測回報覆蓋後最近擷取仍顯示舊縮圖（點「編輯」卻是新圖）**：原判斷「`PathToImageConverter` 無快取」錯誤——`BitmapImage`＋`UriSource` 對相同檔案 URI 命中 WPF 內建解碼影像快取（ImagingCache），原地覆寫後重新解碼仍回傳舊圖；編輯器用 `File.ReadAllBytes`＋SkiaSharp 讀位元組不受影響，與症狀吻合。解法：`PathToImageConverter.Convert` 加 `image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;`（WPF 針對「檔案覆寫後影像不更新」的標準作法），兩頁（最近擷取、剪貼簿歷史）共用此 converter 一併生效
- `CaptureEntity.CreatedAt` 是 UTC `DateTime`：更新時需 `createdAt.ToUniversalTime().UtcDateTime`（對齊 `ToEntity`），直接指派 `DateTimeOffset` 編譯失敗（CS0029）

### 待辦

- 尚未實測大尺寸圖片（超過縮圖 320px 上限）與連續快速點擊的行為
- 升級後既有文字／檔案舊資料僅不顯示，尚未實測長期累積的資料量影響
- 應用程式結束（`App.Current.IsExiting`）時關閉編輯器維持不自動存入，避免結束流程中寫入被截斷
- 編輯後全部復原（內容同原圖）仍會存入一筆（擷取表不去重，與「一律新增」決定一致）

---

## 2026-11-02 - v1.11.0

### 目標

編輯器兩項操作改善：
1. 裁切模式已框選範圍時，「複製」按鈕或 `Ctrl+C` 只複製框出的範圍（含註解合成），不需先確定裁切。
2. 工具下拉選單改為 SplitButton 行為：點擊名稱區直接套用該工具，點擊向下箭頭才向下展開工具選項。

同日追加工具下拉視覺修正：箭頭 hover 看不清楚、使用中工具與選取項的樣式表示。

同日曾規劃「眼睛切換顯示／隱藏註解」功能，經討論後**取消不實作**（含其衍生的複製／裁切／儲存跟隨語意）；並修復複製整張時未包含註解的回歸。

### 變更

- `App/Views/EditorWindow.xaml.cs`：`CopyToClipboardAsync` 以 `_tool == AnnotationTool.Crop && !_cropSelection.IsEmpty` 判斷；有選框時先合成底圖與註解（無註解則直接用底圖）再以 `ImageCodec.Crop` 裁出選取範圍複製；無選框維持複製整張。非破壞性：不套用裁切、不清除選框。
- `App/Strings/AppStrings.cs`：新增 `ClipboardCopiedSelection`；複製選取範圍成功時顯示於狀態列。
- `App/Views/EditorWindow.xaml`：`CopyButton` ToolTip 補上「裁切選取時僅複製選取範圍」；`ToolCombo` 套用 `ToolSelectorStyle`、掛 `PreviewMouseLeftButtonDown` 並加 ToolTip 說明點擊行為。
- `App/Styles/Controls.xaml`：新增 `ToolSelectorStyle`（`x:Key` 的 ComboBox 模板）：透明 `ToggleButton` 只覆蓋右側箭頭欄（寬 28、hover 反白、ToolTip「顯示工具選項」），其餘沿用原 ComboBox 模板；全域 ComboBox 樣式不動，`StrokeCombo` 不受影響。
- `App/Views/EditorWindow.xaml.cs`：新增 `OnToolComboPreviewMouseLeftButtonDown`——點擊落在箭頭鈕內不攔截（正常展開選單）；落在名稱區則 `e.Handled = true` 並 `ApplyTool(SelectedItem)` 套用目前顯示的工具。
- 版本升至 1.11.0（`AssemblyVersion` 1.11.0.0、`FileVersion` 1.11.0.0）。
- 同日工具下拉視覺修正（併入 1.11.0）：
  - `App/Styles/Controls.xaml`：`ToolSelectorStyle` 箭頭 `Path` 移入 `ToggleButton` 模板、畫在 hover 底色之上（`Root` 右欄改固定 28px 空位），hover／下拉展開時箭頭保持可見。
  - `App/Styles/Controls.xaml`：`ToolSelectorStyle` 加 `Tag="active"` 觸發器（accent 填色＋`AccentTextBrush` 前景）；`EditorWindow.xaml.cs` 新增 `UpdateToolFaceState()`，於工具／裁切／橡皮擦切換點設定 Tag——非裁切／橡皮擦模式時名稱區顯示使用中樣式，裁切／橡皮擦模式恢復中性（由那兩個鈕的選取態表示）。
  - `App/Views/EditorWindow.xaml`：`ToolCombo` 圖示 `Path.Stroke` 改綁繼承前景（`AncestorType=Control`）以支援白 icon；`ItemContainerStyle` 改 `BasedOn ToolSelectorItemStyle`（保留 `OnToolItemClicked`）。
  - `App/Styles/Controls.xaml`：新增 `ToolSelectorItemStyle`（`BasedOn ComboBoxItemStyle`）——選取項右側顯示 `AccentBrush` 勾勾並保留 `SelectionBrush` 底色。
- 文件更新：`CHANGELOG.md`、`RELEASE_NOTES.md`、`README.md`（功能特色與下載表）、`docs/USER_GUIDE.md`（裁切框選複製、工具選單兩段式）、`docs/TECHNICAL.md`（5.5 編輯器）、`docs/BUILD_AND_RELEASE.md`（發佈範例版本）。

### 問題與解法

- 全域 ComboBox 模板的透明 `ToggleButton` 覆蓋整個控制項，點擊任何位置都會展開下拉 → 新增 `ToolSelectorStyle` 供 `ToolCombo` 專用，`ToggleButton` 僅覆蓋右側箭頭欄。
- `ToolSelectorStyle` 定義在 `ResourceDictionary`（無 `x:Class`），`EventSetter` 無法綁 code-behind 處理器 → 事件掛在 `ToolCombo` 本體的 `PreviewMouseLeftButtonDown`，以 `IsInsideToolComboToggle` 自事件來源往上尋找 `ToggleButton`，區分箭頭點擊與名稱區點擊。
- `Ctrl+C` 路徑原本就呼叫 `CopyToClipboardAsync`，單一方法修改即同時涵蓋按鈕與快捷鍵。
- **執行期錯誤「Get 屬性 'System.Windows.Controls.ControlTemplate.Triggers' 擲回例外狀況」**（擷取流程顯示「擷取失敗」）：`ToolSelectorStyle` 內層 `ToggleButton` 的 `ControlTemplate.Triggers` 誤寫成 `<Border>` 的子元素（正確應為 `ControlTemplate` 的直接子元素）；WPF 模板於執行期才解析，`dotnet build` 不會報錯 → 移正 triggers 位置後修復。
- 另發現 `Template.FindName("ToggleButton")` 在模板套用初期可能回傳 null → 改以事件來源的視覺/邏輯父鏈判定是否點在箭頭鈕上，不依賴名稱查找。
- 驗證工具：於 `D:\TEMP\opencode\XamlTemplateCheck`（暫存、不入庫）以 `XamlReader` 載入 `Controls.xaml` 並實例化所有樣式模板；破損版可重現同款例外、修正版全數通過，確認修復有效。
- 箭頭 hover 底色（`Toggle Button` 疊層）蓋住箭頭 `Path` → 箭頭移入 `ToggleButton` 模板內、疊在底色之上，`Stroke` 綁 `TemplatedParent` 的 `Foreground`（名稱區 accent 狀態時同步變白）。
- 圖示 `Path` 原以 `DynamicResource TextPrimaryBrush` 寫死，accent 填色時不會變白 → 改綁最近 `Control` 祖先的 `Foreground`（face 為 ComboBox、清單項為 ComboBoxItem），兩種語境各取正確前景色。
- 使用中工具名稱文字仍是黑字壓 accent 底：`Controls.xaml` 開頭的隱式 `<Style TargetType="TextBlock">` 以 setter 設定 `Foreground=TextPrimaryBrush`，**樣式 setter 優先序高於屬性繼承**，`Tag="active"` 觸發器的白色前景傳不到 ItemTemplate 的 `TextBlock`（圖示 `Path` 是顯式 Binding 所以正常）→ 該 `TextBlock` 加顯式 `Foreground` 綁定（同圖示，綁最近 `Control` 祖先）繞過；隱式 TextBlock 樣式全 App 依賴，不動。
- 建置與 `dotnet format` 平行執行時競態存取 `obj/` 產生檔出現 `CS2001 找不到 *.g.cs` → 依序重跑即恢復；驗證指令應循序執行。
- 使用中狀態箭頭仍是黑字：箭頭 `Path.Stroke` 綁 `TemplatedParent`（＝`ToggleButton`）的 `Foreground`，但 `ToggleButton` 的前景未跟隨 ComboBox——被更高優先序的值源（佈景主題預設樣式 setter）阻斷繼承，`Tag="active"` 設在 ComboBox 上的白色前景傳不進去（與前次文字黑字同類問題）→ 改綁最近的 `ComboBox` 的 `Foreground`（不能用 `AncestorType=Control`，最近 Control 是 `ToggleButton` 自己），與名稱文字同一值來源。
- **回歸：複製整張未含註解**（使用者回報「全畫面複製時要包含註解」）：v1.11.0 加框選複製時，`CopyToClipboardAsync` 的整張分支被簡化成直接 `FromSKBitmap(Bitmap)`，漏掉原本「有註解先 `RenderComposite` 合成」的邏輯 → 還原與 `SaveToFileAsync` 一致的判斷（無註解直接轉、有註解合成後轉）；框選複製分支不變。
- 「眼睛切換顯示／隱藏註解」功能經需求訪談後取消不實作（儲存／旋轉配合語意過於複雜，使用者決定整組不做）。
- 箭頭區與名稱區視覺區隔（同日追加，併入 1.11.0）：`ToolSelectorStyle` 外層模板新增 `ToolSplitDivider`（右偏移 28px 對齊箭頭鈕左緣、寬 1、`IsHitTestVisible=False`）——平時 `BorderBrush`、使用中 `AccentTextBrush`＋`Opacity 0.35`；箭頭區 hover／展開底色改狀態感知：平時 `HoverBrush`、使用中 `AccentHoverBrush`。內層 `ToggleButton` 模板無法直接讀 ComboBox 的 `Tag` → 由外層 `Tag` 觸發器以 `TargetName="ToggleButton"` 傳遞 Tag，內層以 `MultiTrigger`（`IsMouseOver`/`IsChecked`＋`Tag=active`）覆蓋平時色。

### 驗證

- `dotnet build Clippings.sln -c Debug`：0 警告、0 錯誤
- `dotnet test Clippings.sln`：全數通過（89 項）
- `dotnet format Clippings.sln --verify-no-changes`：通過
- XAML 模板煙霧測試（`XamlTemplateCheck`，暫存工具）：`Controls.xaml` 全樣式實例化通過
- `dotnet publish`（Release、win-x64、self-contained、ReadyToRun）：成功，`Clippings.exe` FileVersion 1.11.0.0
- `dotnet publish`（Release、win-x64、framework-dependent、無 ReadyToRun）：成功，`Clippings.exe` FileVersion 1.11.0.0
- Release 資產：`Clippings-1.11.0-win-x64.zip`，91,777,757 bytes，SHA-256 `85D064E0D88FA248A9FEFE94BE1AF15B7DFAD9E261BA2F2565895D437E96E4C0`
- Release 資產：`Clippings-1.11.0-win-x64-framework-dependent.zip`，17,349,323 bytes，SHA-256 `943A94992A3E8B596254510F830BCE631FC63D3D849D71F827D8EA5C25A09642`
- GitHub Release v1.11.0：<https://github.com/mark216tw/win-clippings-01/releases/tag/v1.11.0>
- 發佈時使用者的 Debug 版 `Clippings.exe` 正在執行中鎖定輸出，Debug 重建略過（程式碼自上次 build/test/format 全綠後僅變更文件；Release 發佈輸出目錄不受影響且成功編譯驗證）

### 待辦

- 手動測試清單：框選後複製含／不含註解之範圍、無框選時複製整張、複製後選框仍可調整、點工具名稱套用、點箭頭展開選單、箭頭 hover 清晰、使用中工具名稱區 accent 填色、裁切／橡皮擦時名稱區回中性、下拉清單選取項有 accent 勾勾、`StrokeCombo` 下拉正常。
- 儲存（`Ctrl+S`）仍存整張，不隨選框裁切（維持現狀）。

---

## 2026-11-02 - v1.10.2

### 目標

補上圖片編輯器缺少的橡皮擦工具，讓編輯器也能移除已加入的註解；行為與螢幕標註一致，採整筆擦除。

### 變更

- `App/Views/EditorWindow.xaml`：於工具選單（`ToolCombo`）右側新增 `EraserButton` 切換鈕（圖示沿用螢幕標註工具列）；`ToolCombo` 加入 `ItemContainerStyle` 與 `PreviewMouseLeftButtonDown` 事件，處理點選目前項目的情況；`App/Views/EditorWindow.xaml.cs` 新增 `_erasing`／`_eraseSnapshotPushed`／`_toolBeforeEraser` 狀態與 `BeginErase`、`EraseAt`、`EndErase`、`ResetEraseState`、`OnEraserButtonChecked`、`OnEraserButtonUnchecked`、`OnToolItemClicked`，並將工具切換流程抽出為 `ApplyTool`。
- 橡皮擦不再列於工具下拉選單；按下按鈕切換為橡皮擦，再按一次回到工具選單目前選取的工具；切換其他工具或裁切時自動關閉橡皮擦。
- 橡皮擦以 `StrokeHitTester.HitTest` 命中註解，容許誤差 `Math.Max(8f, _strokeWidth * 2f)`；第一次擦到註解時才推入復原快照，一次拖曳合併為單一步驟。
- 擦除時同步移除 `_mosaicCache` 中對應的 `SKBitmap`；切換工具、裁切、`Esc`、失去滑鼠擷取、復原／重做時重設擦除狀態。
- 狀態列新增橡皮擦說明；畫布游標沿用十字游標。
- 版本升至 1.10.2（`AssemblyVersion` 維持 1.10.0.0，`FileVersion` 調整為 1.10.2.0）。
- 將原 1.10.1 的旋轉／翻轉與平移游標變更整併進 1.10.2，`CHANGELOG.md`、`RELEASE_NOTES.md` 不再保留獨立的 1.10.1 版本區塊。
- 文件更新：`README.md`、`docs/USER_GUIDE.md`、`docs/TECHNICAL.md`、`docs/BUILD_AND_RELEASE.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`。

### 問題與解法

- 若在每次滑鼠移動都推快照會灌爆復原堆疊 → 以 `_eraseSnapshotPushed` 確保每次拖曳只推一次，且沒擦到東西時不留空歷史。
- 橡皮擦／裁切是獨立切換鈕，`ToolCombo` 仍顯示原本工具；此時點選同一個工具不會觸發 `SelectionChanged`，導致無法切換 → 在 `ComboBoxItem` 加上 `PreviewMouseLeftButtonDown`，於橡皮擦／裁切狀態下直接以 `ApplyTool` 套用被點選的工具。

### 驗證

- `dotnet build Clippings.sln -c Debug`：0 警告、0 錯誤
- `dotnet test Clippings.sln`：全數通過（89 項）
- `dotnet format Clippings.sln --verify-no-changes`：通過
- `dotnet publish`（Release、win-x64、self-contained、ReadyToRun）：成功，`Clippings.exe` FileVersion 1.10.2.0
- `dotnet publish`（Release、win-x64、framework-dependent、無 ReadyToRun）：成功，`Clippings.exe` FileVersion 1.10.2.0
- Release 資產：`Clippings-1.10.2-win-x64.zip`，95,588,715 bytes，SHA-256 `3AA0518CF923B1A23F04528E76D31377B673F84A514EA8A94FF96C7F84E516C6`
- Release 資產：`Clippings-1.10.2-win-x64-framework-dependent.zip`，18,014,657 bytes，SHA-256 `FEB1E8D29543660C330D86167EB74B57247E06609EEE5AF0907AC5941F76E9EF`
- GitHub Release v1.10.2：<https://github.com/mark216tw/win-clippings-01/releases/tag/v1.10.2>
- GitHub Release v1.10.1 已刪除，其變更整併進 v1.10.2

---

## 2026-11-01 - v1.10.1（已併入 v1.10.2）

> 此版本已於 2026-11-02 整併進 v1.10.2；對應的 GitHub Release v1.10.1 已刪除。

### 目標

編輯器平移游標調整：平移功能預設游標改為 `Arrow`；按下按鍵開始平移時改為 `SizeAll`，讓「可平移中」狀態更明確。（與 1.10.0 兩次變更合併為單一 GitHub Release v1.10.1）

### 變更

- `App/Views/EditorWindow.xaml.cs`：`UpdateCanvasCursor` 的 `AnnotationTool.Pan` 由 `Cursors.Hand` 改 `Cursors.Arrow`（行 1118）；`StartPan` 開始平移時由 `Cursors.Hand` 改 `Cursors.SizeAll`（行 554）
- `StopPan` 原本就會呼叫 `UpdateCanvasCursor()` 還原游標，放開按鍵／Esc 後回到 `Arrow`，不需額外修改
- 版本升至 1.10.1（修訂版，`AssemblyVersion` 維持 1.10.0.0，`FileVersion` 調整為 1.10.1.0）
- 文件更新：`README.md`（功能特色、下載表改 1.10.1）、`docs/USER_GUIDE.md`（編輯器工具表與旋轉／翻轉說明）、`docs/TECHNICAL.md`（`ImageCodec.RotateFlip`、5.5 編輯器）、`docs/SYSTEM_DESIGN.md`（FR-07、Platform.Imaging、底圖說明）、`docs/ARCHITECTURE.md`（`EditorWindow` 職責）補上旋轉／翻轉與平移游標說明；`RELEASE_NOTES.md` 將 1.10.0／1.10.1 併為 v1.10.1 單一 Release 區塊（含下載版本）

### 問題與解法

- 無。平移入口（平移工具左鍵、任意工具中鍵／右鍵）皆經 `StartPan` 單一方法，改一處即涵蓋所有情境。

### 驗證

- `dotnet build Clippings.sln -c Debug`：0 警告、0 錯誤
- `dotnet test Clippings.sln`：全數通過
- `dotnet format Clippings.sln --verify-no-changes`：通過
- `dotnet publish`（Release、win-x64、self-contained、ReadyToRun）：成功，`Clippings.exe` 版本 1.10.1.0
- `dotnet publish`（Release、win-x64、framework-dependent、無 ReadyToRun）：成功，`Clippings.exe` 版本 1.10.1.0
- Release 資產：`Clippings-1.10.1-win-x64.zip`，95,586,179 bytes，SHA-256 `07CB0F8D599056D778918C014D45CF210A77F16A3310DEBB604EFF7596B09A2E`
- Release 資產：`Clippings-1.10.1-win-x64-framework-dependent.zip`，18,013,771 bytes，SHA-256 `6784B1A2D99EB6846374CEBC3CC6FCF3588A0ADE313894BCC9BA2882FE3367F0`
- GitHub Release v1.10.1：<https://github.com/mark216tw/win-clippings-01/releases/tag/v1.10.1>

### 待辦

- 尚未實機確認游標切換時機（按下／放開）手感，如有需要可改為拖曳超過門檻才切換 `SizeAll`。
- Release ZIP 未在乾淨 Windows 環境（完整版）與已裝 .NET 10 Desktop Runtime 環境（精簡版）實測。

---

## 2026-11-01 - v1.10.0

### 目標

編輯器工具列新增旋轉／翻轉功能（右轉、左轉、垂直翻轉、水平翻轉），置於裁切按鈕左邊；裁切按鈕由文字改為圖示。依使用者決定：註解先「合併」進圖片再變形、立即套用＋可復原、不加快捷鍵。

### 變更

- `Core/Editing/RotateFlipKind.cs`（新增）：變形型別 `Rotate90Cw`／`Rotate90Ccw`／`FlipHorizontal`／`FlipVertical`
- `Platform/Imaging/ImageCodec.cs`：新增 `RotateFlip`（90° 旋轉以 `Translate`+`RotateDegrees`、翻轉以 `Translate`+`Scale` 繪製，Nearest 取樣像素精確；旋轉時輸出寬高互換）
- `App/Views/EditorWindow.xaml`：裁切鈕左邊新增右轉／左轉／垂直翻轉／水平翻轉圖示鈕（`EditorActionButtonStyle`＋自訂 Path，沿用復原鈕的圖示慣例）；裁切鈕 `Content` 改裁切圖示 Path，並加 `Padding="10,6"` 配合圖示尺寸；ToolTip 皆標註「註解將合併」
- `App/Views/EditorWindow.xaml.cs`：新增 `ApplyRotateFlip` 與四個 `Click` 處理器；`RenderComposite` 重構為帶背景色參數（複製／儲存仍傳白色、不變行為，合併傳透明以保留 PNG 透明度）
- `Styles/Controls.xaml`：`ToolbarToggleStyle` 模板 `TextBlock Text="{TemplateBinding Content}"` 改 `ContentPresenter`，以支援圖示內容（此樣式僅裁切鈕使用，選取態 `AccentBrush` 邏輯不變）
- 版本升至 1.10.0（主次版變更，`AssemblyVersion` 一併調整為 1.10.0.0）

### 問題與解法

- `ToolbarToggleStyle` 模板以 `TextBlock` 呈現 `Content`，放 Viewbox＋Path 顯示不出來 → 模板改 `ContentPresenter`；圖示筆刷以 `RelativeSource` 綁 `ToggleButton.Foreground`，選取態自動變淺色圖示。
- 合併註解若沿用 `RenderComposite` 的白色清除會蓋掉 PNG 透明區域 → 攃出背景色參數，合併路徑傳 `SKColors.Transparent`，原圖 alpha 保留。
- 裁切模式中點旋轉／翻轉會殘留選取框 → `ApplyRotateFlip` 開頭先提交文字輸入、取消 `CropButton` 選取、`ClearCropSelection()` 與 `StopPan` 再套用。
- 一次復原要能同時救回註解與方向 → 合併與變形前只推一次 `PushCurrentSnapshot()`，Ctrl+Z 回到變形前（註解仍可編輯）。

### 驗證

- `dotnet build Clippings.sln -c Debug`：0 警告、0 錯誤
- `dotnet test Clippings.sln`：全數通過
- `dotnet format Clippings.sln --verify-no-changes`：通過

### 待辦

- 旋轉／翻轉會把註解合併進圖片（依使用者決定），之後註解不可再單獨編輯（復原可救回）。
- 未提供 180° 旋轉與鍵盤快捷鍵（使用者要求僅放按鈕）。
- 新增圖示鈕僅編譯驗證，尚未實機目視確認圖示比例與間距。

---

## 2026-11-01 - v1.9.0

### 目標

新增「螢幕標註」模式：直接在 Windows 畫面上即時標註（雷射筆、畫筆、螢光筆、直線、箭頭、矩形、橢圓、橡皮擦、清空、線寬／顏色選擇、浮動工具列），並與既有截圖功能整合。經評估後整合進 Clippings（複用繪圖核心、熱鍵、系統匣、單一實例），不另建新 AP。

### 變更

- `Helpers/AnnotationRenderer.cs`（新增）：從 `EditorWindow` 抽出 Skia 繪圖核心（畫筆／螢光筆／直線／箭頭／矩形／橢圓／文字／編號／馬賽克／雷射軌跡），馬賽克以 resolver 回呼保留編輯器快取；`EditorWindow` 改用共用版（行為不變）
- `Core/Models/AnnotationTool.cs`：新增 `Laser`（11）、`Eraser`（12），既有枚舉值不變
- `Core/Models/AnnotationSettings.cs`、`AnnotationBackdrop.cs`（新增）：標註預設值（工具／顏色／線寬／游標穿透／背景：跟隨畫面・白板・黑板）
- `Core/Models/AppSettings.cs`：新增 `HotkeyAnnotate`（預設 `Ctrl+Shift+A`）與 `Annotation`；`Clone` 改深層複製
- `Core/Editing/LaserTrail.cs`（新增）：雷射筆漸隱軌跡（預設 0.8 秒生命週期、去重、過期裁剪）
- `Core/Editing/StrokeHitTester.cs`（新增）：橡皮擦整筆命中測試（折線／線段／矩形邊界／橢圓輪廓／筆章範圍）
- `Platform/Interop/NativeMethods.cs`、`WindowHelper.cs`：`Get/SetWindowLongPtr` 與 `SetClickThrough`（`WS_EX_TRANSPARENT` 點擊穿透）
- `App/Services/AnnotationDocument.cs`（新增）：跨螢幕筆畫文件＋`UndoStack`（清空可復原；連續擦除合併為單一步驟）
- `App/Services/ScreenAnnotationService.cs`（新增）：工作階段生命週期（每螢幕一 overlay＋單一浮動工具列）、工具狀態、截圖協調介面
- `App/Views/AnnotationOverlayWindow.xaml(.cs)`（新增）：每螢幕透明繪圖層（`AllowsTransparency`＋`SKElement`），背景採 `#01000000`（alpha=1，見下方問題與解法）、拖曳繪製、右鍵取消筆畫、Esc 結束、Ctrl+Z/Y 復原重做
- `App/Views/AnnotationToolbarWindow.xaml(.cs)`（新增）：浮動工具列（拖曳把手、8 種工具、16 色色票、線寬 2/4/6/8/12、背景、游標模式、復原／重做／清空／截圖／結束）；「游標」按鈕採 `ToggleButton`＋游標箭頭圖示（可開／關切換、位於橡皮擦右邊），選到繪圖工具時自動退出游標模式
- `Styles/Controls.xaml`：新增 `AnnotateToolToggleStyle`（RadioButton）與 `AnnotatePointerToggleStyle`（ToggleButton），選取態以 `AccentBrush` 藍底＋淺色圖示提升對比
- 工具列 UI 打磨：復原／重做改為圖示鈕（沿用編輯器 Path）；新增「顯示／隱藏標註」眼睛鈕（eye／eye-off 圖示切換）；「結束」改為工具列右上角 X 圖示（`DockPanel.Dock="Right"`）；雷射筆圖示換四芒星星芒；拖曳把手加 `Background="Transparent"` 使整塊可拖；工具列空白處按住左鍵也可 `DragMove`（`OnBarMouseDown` 排除 `ButtonBase`／`ComboBox`／`Popup`）；工具鈕改掛 `Click`（已選工具再點也觸發，配合自動退出游標模式）；hover 背景 `HoverBrush` → `#33FFFFFF` 提升圖示對比；第一列結尾順序為「眼睛（顯示/隱藏標註）」→「游標」
- `ScreenAnnotationService`：新增 `StrokesVisible`／`ToggleStrokesVisible`（會期內狀態，不持久化；隱藏時略過已提交筆畫渲染，草稿與雷射軌跡仍顯示）
- 雷射筆尾巴縮短：生命週期 800ms → 400ms（`AnnotationOverlayWindow.LaserLifetimeMilliseconds` 常數，可再調）
- 工具固定快捷鍵（數字＋字母、含 NumPad）：1/L 雷射筆、2/P 畫筆、3/H 螢光筆、4/N 直線、5/A 箭頭、6/R 矩形、7/O 橢圓、8/E 橡皮擦、0/C 游標模式、9/V 顯示隱藏；由 `ScreenAnnotationService.TryHandleShortcut` 統一派發，overlay 與工具列 `OnKeyDown` 共用；tooltip 顯示快捷鍵；焦點在 `TextBox`／`ComboBox`／`Popup` 時不攔截按鍵
- `NativeMethods`／`WindowHelper`：新增 `HwndTopmost`、`SwpNoMove`／`SwpNoSize` 與 `BringToTopMost`；overlay `Activated` 時把工具列抬回 topmost 最上層，避免繪圖後工具列被 overlay 蓋住
- `CaptureCoordinator`：截圖前藏工具列（不入鏡）、區域框選時暫藏 overlay（標註已烤進截圖）、開啟編輯器前先結束標註（避免編輯器被置頂 overlay 蓋住）
- `App.xaml.cs`：DI 註冊、`Ctrl+Shift+A` 熱鍵、系統匣「螢幕標註」、`--annotate`、結束程式時停止標註
- `StartupCommand`／`CommandLineOptions`／`AppStrings`：新增 `--annotate` 與說明
- `SettingsPage`：第 5 組熱鍵（螢幕標註）＋螢幕標註預設值卡（游標模式、背景），含重複熱鍵檢查
- 測試：新增 21 個（`LaserTrailTests` 6、`StrokeHitTesterTests` 8、`AppSettingsTests` 5、`CommandLineOptionsTests` 2）
- 版本升至 1.9.0（主次版變更，`AssemblyVersion` 一併調整為 1.9.0.0）

### 問題與解法

- 執行中的 `Clippings.exe` 鎖住 `bin` 輸出導致建置失敗（MSB3027）→ 請使用者手動關閉後重建即通過。
- 繪圖碼若在編輯器與 overlay 各寫一份會重複 → 抽出 `AnnotationRenderer`；馬賽克需要原圖來源，改以 `Func<Annotation, SKRect, SKBitmap?>` resolver 注入，編輯器保留快取邏輯。
- 橡皮擦拖曳時每一個滑鼠移動都推 Undo 快照，會把復原堆疊灌爆 → `AnnotationDocument` 以 `_lastOpWasErase` 將連續擦除合併為單一復原步驟。
- 標註 overlay 是 Topmost，會蓋住擷取 overlay 與編輯器 → 截圖流程先 BitBlt（標註已包含在畫面中）再暫藏 overlay 進行框選；開啟編輯器前先 `Stop()` 結束標註。
- `AppSettings.Clone` 原為 `MemberwiseClone`，新增嵌套 `Annotation` 物件後會被共用 → 改深層複製並加測試防呆。
- `Mouse.Capture` 需作用在可命中測試的元素上，`SKElement` 設 `IsHitTestVisible=false` → 捕捉目標改為背景 `RootGrid`。
- 實測發現游標模式無法關閉、整個標註固定在游標模式 → 「游標」按鈕誤用 `RadioButton`，WPF 的 `RadioButton` 點選後不能自我取消，`Unchecked` 永不觸發導致 `WS_EX_TRANSPARENT` 無法移除 → 改用 `ToggleButton`（`ToolbarToggleStyle` 本來就是 `TargetType="ToggleButton"`，直接沿用），並在 `ScreenAnnotationService.SetTool` 加入「選到繪圖工具時自動退出游標模式」。
- 修正後標註模式仍完全吃不到滑鼠、點擊穿透到底層（`settings.json` 確認 `clickThrough: false`，狀態沒卡住）→ `AllowsTransparency=True` 的 WPF 視窗底層是 `UpdateLayeredWindow` 型 layered window，Windows 對這類視窗做**逐像素 alpha 命中測試**，alpha=0 的像素滑鼠直接穿透；overlay 全透明（`Background="Transparent"`＋畫布 `SKColors.Transparent`）導致整個視窗收不到任何滑鼠事件 → 背景改為 `#01000000`、畫布清空色改 `new SKColor(0,0,0,1)`（alpha=1/255 肉眼不可視），命中測試即視為可點擊；`WS_EX_TRANSPARENT` 保留作為游標模式的跨程序穿透機制。備案（未動用）：`HwndSource` 掛 `WM_NCHITTEST` 回傳 `HTCLIENT` 繞過逐像素命中測試。
- 繪圖後工具列無法操作 → overlay 與工具列同為 topmost，點擊 overlay 使其成為作用中視窗後被抬到 topmost 最上方、蓋住工具列 → overlay 掛 `Activated` 事件，以 `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)` 把工具列抬回最上層（不搶焦點）。
- 已選工具看不清楚 → `ToolToggleStyle` 選取態為淺色 `SelectionBrush` 底＋淺色圖示 → 新增 `AnnotateToolToggleStyle`／`AnnotatePointerToggleStyle`，選取態改 `AccentBrush` 藍底。
- 建置時 `WindowHelper.BringToTopMost` 引用未宣告的 `SwpNoMove`／`SwpNoSize`（CS0117）→ 於 `NativeMethods` 補上常數。
- 游標模式下點擊「已選的工具」無反應、無法退回標註模式 → RadioButton 已選再點不觸發 `Checked` → 工具鈕改掛 `Click` 事件（每次點擊皆觸發），搭配自動退出游標模式。
- 移動把手只有一小部分可拖 → `DragGrip` 的 Grid 未設 `Background`，僅小圓點可命中 → 加 `Background="Transparent"` 使整塊可命中。
- 工具圖示 hover 背景太淺看不清 → hover 由 `HoverBrush` 改 `#33FFFFFF`（深底微亮）。
- 以移動把手拖曳工具列跳出「當主要滑鼠按鈕已按下時，只能呼叫 DragMove」→ `OnDragGripDown` 的 `DragMove()` 結束（滑鼠放開）後事件未設 `Handled`、繼續冒泡到 `BarRoot` 的 `OnBarMouseDown` 再呼叫一次 `DragMove()` → 拋出例外 → `OnDragGripDown` 設 `e.Handled = true` 阻斷冒泡，兩處並加 `Mouse.LeftButton == Pressed` 防護。

### 驗證

- `dotnet build Clippings.sln -c Debug`：0 警告、0 錯誤
- `dotnet test Clippings.sln`：89 測試通過（原 68 ＋ 新增 21）
- `dotnet format Clippings.sln --verify-no-changes`：通過
- 實機測試多輪：`--annotate` 啟動、標註繪圖、游標模式開／關、工具列操作、快捷鍵、顯示／隱藏、拖曳移動皆經人工確認；過程中修正逐像素命中測試、工具列 z-order、DragMove 冒泡重複呼叫等問題（見上方問題與解法）
- `dotnet publish`（Release、win-x64、self-contained、ReadyToRun）：成功，`Clippings.exe` 版本 1.9.0.0
- `dotnet publish`（Release、win-x64、framework-dependent、無 ReadyToRun）：成功，`Clippings.exe` 版本 1.9.0.0
- Release 資產：`Clippings-1.9.0-win-x64.zip`，95,583,329 bytes，SHA-256 `B1B3FEE058BD8EDCBE9636DA247EA78F70A7C3EE202659EC2428B80973A95EF5`
- Release 資產：`Clippings-1.9.0-win-x64-framework-dependent.zip`，18,012,713 bytes，SHA-256 `ABE3CB1DEF866217D05F647758049B18EBBF8EEF450000EE1BFCED10548A7E3D`
- GitHub Release v1.9.0：<https://github.com/mark216tw/win-clippings-01/releases/tag/v1.9.0>

### 待辦

- 混合 DPI 多螢幕的螢幕標註尚未實測（單一 100% 縮放螢幕驗證通過）。
- 筆畫不能跨越螢幕邊界（每螢幕獨立畫布）。
- 游標模式（點擊穿透）下雷射筆不顯示軌跡（無滑鼠事件）。
- 主視窗尚未加入標註入口按鈕（目前為熱鍵／系統匣／CLI）。
- 標註工具列文字未移至 `AppStrings`。

---

## 2026-11-01 - v1.8.5

### 目標

調整編輯器工具順序，預設改為平移，並記住使用者上次使用的工具。

### 變更

- `EditorWindow`：工具順序改為平移、畫筆、螢光筆、文字、編號、直線、箭頭、矩形、橢圓、馬賽克
- `EditorWindow`：預設工具改為平移，開啟時依 `AppSettings.LastTool` 還原上次工具
- `EditorWindow`：切換工具時呼叫 `AppState.RememberLastTool`；裁切為獨立按鈕，不列入記憶
- `AppSettings`：新增 `LastTool`（預設 `AnnotationTool.Pan`）
- `AppState`：新增 `RememberLastTool`，僅在值變更時寫入設定檔，不觸發 `SettingsChanged`
- `CaptureCoordinator`、`HistoryPage`：改傳 `AppState` 給 `EditorWindow`
- `AppSettingsTests`：新增 `LastTool` 預設值與 `Clone` 保留測試
- 版本升至 1.8.5（`AssemblyVersion` 依規範維持 1.8.1.0）

### 問題與解法

- 直接呼叫 `AppState.Save` 會在每次切換工具時重套主題與重寫開機登錄；改為 `RememberLastTool` 只寫設定檔、不觸發事件，避免副作用。
- `LastTool` 若為非工具選單項目（例如 `Crop`）或無效值，建構時回退為平移。

### 驗證

- `dotnet build Clippings.sln -c Debug`：0 警告、0 錯誤
- `dotnet test Clippings.sln --no-build`：68 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- `dotnet publish`（Release、win-x64、self-contained、ReadyToRun）：成功，`Clippings.exe` 版本 1.8.5
- `dotnet publish`（Release、win-x64、framework-dependent、關閉 ReadyToRun）：成功，`Clippings.exe` 版本 1.8.5
- 精簡版 `Clippings.exe --help`：正常輸出使用說明
- Release 資產：`Clippings-1.8.5-win-x64.zip`，95,538,012 bytes，SHA-256 `0267DAEB372D0BC8AB469C2603CC1C2C42777E9A5AB50A9172F8CDDFA7C33E38`
- Release 資產：`Clippings-1.8.5-win-x64-framework-dependent.zip`，17,997,919 bytes，SHA-256 `4E94AF5904221A034670E9F3016B0BF67F118DDA1C7229B290046F8BEDD57CD1`
- GitHub Release v1.8.5：<https://github.com/mark216tw/win-clippings-01/releases/tag/v1.8.5>

### 待辦／已知限制

- 尚未實測跨程式重啟後是否正確還原工具，以及深色主題下的工具選單外觀

---

## 2026-11-01 - v1.8.4（補充發佈）

### 目標

新增較小的 framework-dependent 版本，讓已安裝 .NET 10 Desktop Runtime 的使用者不必下載約 95 MB 的完整免安裝版。

### 變更

- 新增發佈指令：`dotnet publish -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=false`
- 新增 Release 資產：`Clippings-1.8.4-win-x64-framework-dependent.zip`（33,364,870 bytes）
- `README.md`：新增完整版／精簡版下載表與 .NET 10 Desktop Runtime 需求
- `docs/BUILD_AND_RELEASE.md`：補充兩種發佈、分開輸出目錄、打包與 Release 指令
- `CHANGELOG.md`、`RELEASE_NOTES.md`：補上精簡版說明
- 未變更程式碼，版本維持 1.8.4

### 問題與解法

- `dotnet publish` 預設輸出目錄與 self-contained 相同，先前的完整版會被覆蓋；文件改以 `-o artifacts\publish-win-x64-full` 與 `-o artifacts\publish-win-x64-fx` 分開輸出。
- 精簡版仍包含 `Microsoft.Windows.SDK.NET.dll`（原始 52.75 MB）與 SkiaSharp／SQLite 原生函式庫；這些是目標框架與套件相依，未手動移除。
- PDB（尤其 `libSkiaSharp.pdb`，91.75 MB）不納入 ZIP，避免體積暴增。

### 驗證

- `dotnet publish`（framework-dependent、win-x64、關閉 ReadyToRun）：成功，`Clippings.exe` 版本 1.8.4
- 精簡版 ZIP：33,364,870 bytes，SHA-256 `F29279335B0CB6405129D9932CBDBD8C2FB583E18A1CA3037DAE023D348C25BD`
- 解壓縮後執行 `--help`：輸出正確、退出碼 0
- 解壓縮後冷啟動 `--autostart`，再執行 `--show`：第二實例退出碼 0，維持單一實例
- 建置、66 項測試與格式檢查沿用同一版程式碼結果

### 待辦／已知限制

- 尚未在未安裝 .NET 10 Desktop Runtime 的乾淨機器驗證缺少執行階段的提示
- 精簡版仍需目標電腦安裝 .NET 10 Desktop Runtime（x64），不是零依賴
- 發佈檔未簽章，首次執行可能出現 SmartScreen 提示

---

## 2026-10-31 - v1.8.4

### 目標

提供最小可用的命令列參數，讓腳本或捷徑能觸發既有實例顯示視窗或擷取，並保留單一實例與常駐行為。

### 變更

- 新增 `Clippings.Core/Startup/`：`StartupCommand` 列舉與 `CommandLineOptions` 解析器（大小寫不敏感、`--help` 優先、重複指令視為錯誤）
- `SingleInstanceService`：管線訊息由 1 byte 改為 UTF-8 JSON 單行（上限 1024 bytes），轉送參數並以 `ActivationRequestedEventArgs` 回傳
- 新增 `ConsoleBridge` 與 `AttachConsole` P/Invoke；`--help`／錯誤訊息在可附加父主控台時輸出，否則以 `MessageBox` 顯示
- `App.OnStartup`：解析參數、`--help`／錯誤提前結束、第二實例轉送後以 0／1 結束、冷啟動擷取不顯示主視窗
- `CaptureInvocationSource` 新增 `CommandLine`；CLI 擷取不自動開編輯器、不播放提示音
- `AppStrings`：新增用法與錯誤文案
- `README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`：補充命令列說明
- 新增繁體中文專案文件：`docs/USER_GUIDE.md`、`docs/ARCHITECTURE.md`、`docs/TECHNICAL.md`、`docs/SYSTEM_DESIGN.md`、`docs/BUILD_AND_RELEASE.md`、`docs/PRIVACY.md`、`docs/README.md`
- 新增 `LICENSE`（MIT）、`CONTRIBUTING.md`、`SECURITY.md`，README 改寫為專案入口
- 建立公開發行：<https://github.com/mark216tw/win-clippings-01>
- GitHub Release v1.8.4：<https://github.com/mark216tw/win-clippings-01/releases/tag/v1.8.4>
- 版本升至 1.8.4（`AssemblyVersion` 依規範維持 1.8.1.0）

### 問題與解法

- WPF 為 `WinExe`，啟動時沒有主控台，直接 `Console.WriteLine` 不會顯示；改用 `AttachConsole(ATTACH_PARENT_PROCESS)` 並設定 UTF-8，附加失敗才退回 `MessageBox`。
- PowerShell 對 GUI 程式不會自動等待，讀不到退出碼；文件改以 `Start-Process -Wait -NoNewWindow -PassThru` 示範。
- 原本 `--autostart` 是先 `Show()` 再 `Hide()`，冷啟動可能閃一下；改為依 `HideMainWindow` 直接不顯示。
- 舊版管線只寫 1 byte，新版讀到無效 JSON 時降級為空參數（顯示主視窗），避免版本混用時卡住。
- `--autostart` 轉送給既有實例時原本會彈窗；改為依參數判斷，不再顯示視窗。

### 驗證

- `dotnet build Clippings.sln`：0 警告、0 錯誤
- `dotnet test Clippings.sln --no-restore`：66 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- 手動：`--help` 輸出正確、退出碼 0；`--bogus` 輸出錯誤與用法、退出碼 2
- 手動：冷啟動 `--autostart` 後執行 `--show`，第二實例退出碼 0，程序維持單一實例
- `dotnet publish`（Release、win-x64、self-contained、ReadyToRun）：成功，`Clippings.exe` 版本 1.8.4
- Release 資產：`Clippings-1.8.4-win-x64.zip`，95,537,020 bytes，SHA-256 `9BCA5DFF94B28A506C83409AFD6EB6CF98594005766E5E3C7E6243F98C915EAD`
- 已下載 Release ZIP 解壓縮並執行 `--help`：輸出正確、退出碼 0
- GitHub 正確辨識授權為 MIT，儲存庫為公開
- 文件連結與繁體中文用字人工檢查

### 待辦／已知限制

- 尚未實測 CLI 觸發的實際擷取流程，避免測試時覆寫剪貼簿與歷史庫
- 尚未實測無父主控台時的 `--help`／錯誤 `MessageBox` 備援
- `--capture-window` 會擷取觸發當下的前景視窗，從終端機呼叫通常抓到終端機
- 轉送為非同步，退出碼 0 不代表擷取完成；未提供 `--wait` 或輸出路徑

---

## 2026-10-31 - v1.8.3

### 目標

在編輯器的復原、重做與符合視窗按鈕加入向量圖示，提升辨識度並保留原本文字。

### 變更

- `Controls.xaml`：新增 `EditorActionButtonStyle`，以 `ContentPresenter` 支援圖示與文字並存
- `EditorWindow.xaml`：三顆按鈕改為 `Path`＋`TextBlock`，圖示顏色綁定按鈕前景色
- `EditorWindow.xaml`：補上三顆按鈕的 `AutomationProperties.Name`
- `README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`：更新說明
- 版本升至 1.8.3（`AssemblyVersion` 依規範維持 1.8.1.0）

### 問題與解法

- `BaseButtonStyle` 的樣板以 `TextBlock` 呈現 `Content`，無法顯示圖示；若直接改全域樣板會影響其他按鈕的文字顏色與懸停效果，因此僅為編輯器新增專用樣式。
- 應用程式有隱含 `TextBlock` 樣式會覆寫繼承的前景色；圖示與文字的顏色改為明確綁定按鈕 `Foreground`，確保懸停與停用狀態一致。

### 驗證

- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- `dotnet test Clippings.sln --no-restore`：48 項通過
- Debug 建置：0 警告、0 錯誤
- 執行期檢查：三顆按鈕內容含 `Path` 與 `TextBlock`，樣式套用成功
- 執行期檢查：圖示與文字顏色皆等於按鈕前景色，且變更前景色後同步更新
- 執行期檢查：視窗寬度 900 時工具列中央區未被裁切（實際 219 / 需要 219）

### 待辦／已知限制

- 尚未在各主題與高 DPI 縮放下實測圖示線條清晰度

---

## 2026-10-31 - v1.8.2

### 目標

調整編輯器工具順序，將裁切移出工具選單並放到線寬右側，同時修正設定頁音效文字。

### 變更

- `EditorWindow`：工具順序改為平移、直線、矩形、箭頭、橢圓、畫筆、螢光筆、文字、編號、馬賽克
- `EditorWindow`：移除工具選單中的裁切，新增線寬右側 `CropButton`（ToggleButton）
- `EditorWindow`：裁切模式改用 `_toolBeforeCrop` 記錄與還原，切換工具時自動離開裁切
- `EditorWindow`：預設工具改以工具值選取箭頭，避免依賴固定索引
- `Controls.xaml`：新增 `ToolbarToggleStyle`，選取狀態使用強調色
- `SettingsPage`：音效選項文字改為「使用快捷鍵擷取完成時播放音效」
- 版本升至 1.8.2（`AssemblyVersion` 依規範維持 1.8.1.0）

### 問題與解法

- 工具順序調整後若仍使用固定索引，預設工具會變成直線；改以 `AnnotationTool.Arrow` 反查項目。
- 裁切改為獨立按鈕後，需避免與工具選單狀態互相覆蓋；選單變更時先取消裁切勾選，再由新工具覆寫狀態。

### 驗證

- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- `dotnet test Clippings.sln --no-restore`：48 項通過
- Debug 建置：0 警告、0 錯誤
- 執行期檢查：工具選單 10 項，enum 順序為 10,1,2,0,3,4,5,6,8,7（平移、直線、矩形、箭頭、橢圓、畫筆、螢光筆、文字、編號、馬賽克）
- 執行期檢查：預設選取箭頭（index 3）、裁切按鈕樣式成功套用、啟用裁切顯示裁切提示
- 執行期檢查：切換工具會自動取消裁切；關閉裁切後回復前一個工具與狀態文字

### 待辦／已知限制

- 尚需在深色與各舒適主題下實測裁切按鈕的強調色對比

---

## 2026-10-31 - v1.8.1

### 目標

將快捷鍵擷取完成音效改為 Windows 內建音效，移除隨程式內嵌的音效資產。

### 變更

- `CaptureCoordinator`：改以 `SystemSounds.Asterisk.Play()` 播放系統提示音
- `CaptureCoordinator`：移除 `SoundPlayer`、資源載入與串流欄位，取消不必要的 `IDisposable`
- `Clippings.App.csproj`、`Assets/capture.wav`：移除內嵌音效資源與檔案
- `SettingsPage`：音效設定文案改為「系統提示音」
- 版本升至 1.8.1

### 問題與解法

- 內嵌音效不受 Windows 音量與音效配置影響，且增加資產與載入失敗風險；改用系統提示音可遵循使用者設定並簡化程式。

### 驗證

- `dotnet test Clippings.sln --no-restore`：48 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- Debug 建置：0 警告、0 錯誤（以暫存 artifacts 目錄完成，避開執行中程式的輸出鎖定）

### 待辦／已知限制

- 若使用者在 Windows 停用該系統音效事件，擷取完成時可能沒有聲音

---

## 2026-10-31 - v1.8.0

### 目標

新增目前可見前景視窗擷取，並提供主視窗、系統匣與可自訂全域快捷鍵入口。

### 變更

- `CaptureMode`、`AppSettings`：新增視窗擷取模式與預設 `Ctrl+Shift+W` 快捷鍵
- `NativeMethods`、`WindowHelper`：加入前景視窗、PID、DWM 可見框線、最小化與 cloaked 狀態辨識
- `ForegroundWindowTracker`：以 WinEvent hook 追蹤最近有效的非 Clippings 前景視窗
- `ScreenCaptureService`：加入指定虛擬桌面矩形擷取，支援負座標、跨螢幕及可見範圍裁切
- `CaptureCoordinator`：擷取前保存並在隱藏 Clippings 後重新驗證同一個視窗目標
- `MainWindow`、系統匣、`SettingsPage`：新增目前視窗入口、快捷鍵註冊與設定
- 測試新增 enum 穩定性、快捷鍵設定複製及視窗與虛擬桌面相交案例
- 版本升至 1.8.0

### 問題與解法

- 從 Clippings 主視窗或系統匣操作時，當下前景視窗可能是 Clippings 或工作列；改為持續追蹤並保存上一個有效外部視窗。
- HWND 可能在擷取等待期間關閉或被重用；保存 HWND 與 PID，真正擷取前再次驗證視窗狀態與 PID。
- `GetWindowRect` 可能包含透明 resize border；優先使用 `DWMWA_EXTENDED_FRAME_BOUNDS`，失敗時才 fallback。

### 驗證

- `dotnet test Clippings.sln --no-restore`：48 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- Win32 整合檢查：成功取得目前外部視窗的 HWND、PID 與非空 DWM 邊界
- 主視窗、設定頁與編輯器型別靜態初始化：通過
- Debug 建置：0 警告、0 錯誤

### 待辦／已知限制

- 採用桌面可見畫面擷取；最小化、被置頂視窗遮擋、移出螢幕或受保護內容可能不完整
- Windows.Graphics.Capture 離屏擷取、混合 DPI 多螢幕仍待後續完整實測

---

## 2026-10-31 - v1.7.0

### 目標

讓全域快捷鍵擷取完成後直接進入編輯器並提供聲音回饋，同時讓使用者可關閉音效，並改善編輯器目前顏色的辨識度。

### 變更

- `CaptureCoordinator`、`App.xaml.cs`：加入擷取來源，僅全域快捷鍵擷取會自動開啟編輯器及播放快門音
- `Assets/capture.wav`、`Clippings.App.csproj`：加入原創短促 PCM 快門音並內嵌為 WPF Resource
- `AppSettings`、`SettingsPage`：新增預設啟用的快捷鍵擷取音效設定，完整支援載入、儲存與未儲存變更偵測
- `EditorWindow`：顏色按鈕改為整體顯示目前顏色，並依亮度自動切換文字顏色
- `AppSettingsTests`：驗證音效設定預設值及複製獨立性
- 版本升至 1.7.0

### 問題與解法

- 主視窗、系統匣與全域快捷鍵原本共用相同擷取入口，無法只調整快捷鍵行為；新增明確來源參數，避免其他入口意外自動開啟編輯器。
- 音效不能影響已完成的擷取；資源載入與播放皆包在獨立錯誤處理中，失敗只記錄警告。
- 固定音效若引用外部素材會有授權與發佈風險；改為自行合成短 PCM WAV 並內嵌至組件。
- 建置與測試同時執行時競爭 `bin/obj`，造成暫時檔案鎖定；改為測試完成後單獨重跑建置即通過。

### 驗證

- `dotnet test Clippings.sln --no-restore`：42 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- 快門音資源：成功從組件載入並通過 `SoundPlayer.Load()` 解析
- 編輯器靜態初始化：工具 Geometry 全部成功解析
- Debug 建置：0 警告、0 錯誤

### 待辦／已知限制

- 全域快捷鍵與實際音訊輸出仍需在具有音效裝置的 Windows 工作階段實測

---

## 2026-10-31 - v1.6.0

### 目標

限制編輯內容只能位於圖片內，改善工具辨識、顏色預覽與快速平移，並將裁切改為可調整且需確認的非破壞性操作流程。

### 變更

- `EditorWindow.xaml`：工具選單加入 11 個專屬向量圖示，顏色選單加入目前顏色預覽
- `EditorWindow.xaml`：加入裁切虛線選框、八方向手把與浮動確定／取消操作列
- `EditorWindow.xaml.cs`：限制註解起點與拖曳座標位於圖片內，並裁切 Skia 與文字輸入覆蓋區域
- `EditorWindow.xaml.cs`：新增裁切選取、移動、縮放、重畫、確認與取消狀態流程
- `EditorWindow.xaml.cs`：任意工具支援中鍵或右鍵快速平移，裁切支援 Enter／Esc 鍵盤操作
- 版本升至 1.6.0

### 問題與解法

- 舊裁切在放開滑鼠時立即修改影像，無法調整範圍；改為保留 `PixelRect` 選取狀態，只有確認時才執行裁切並推入一筆復原紀錄。
- 畫布座標可能落在圖片外；開始操作前檢查圖片範圍，拖曳期間將影像座標限制到邊界，繪製階段再以 Skia clip 防止越界。
- 裁切選框會隨縮放及平移失去位置；統一由影像座標轉換為畫布座標，並在視窗尺寸、縮放、平移及裁切後更新覆蓋層。

### 驗證

- 編輯器靜態初始化：11 個工具 Geometry 全部成功解析
- `dotnet test Clippings.sln --no-restore`：41 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- Debug 建置：0 警告、0 錯誤

### 待辦／已知限制

- 尚未在混合 DPI 多螢幕環境實測裁切手把與浮動操作列位置

---

## 2026-10-31 - v1.5.2

### 目標

修正螢光筆沿著筆畫出現一圈一圈深色接點的問題。

### 變更

- `EditorWindow`：螢光筆改為以 `SKPathBuilder` 建立單一路徑後一次繪製
- `EditorWindow`：螢光筆先以不透明明色畫入 `SaveLayer`，還原圖層時才整體套用 30% 透明度
- 版本升至 1.5.2

### 問題與解法

- 舊畫法以 `DrawPoints(SKPointMode.Polygon)` 繪製帶 alpha 的粗筆畫，密集取樣點的半透明圓端在同一像素重複混色，接點因而變深，形成一圈一圈。
- 以 Skia 實際渲染比對：舊畫法沿線 alpha 為 224–247 不平均；新畫法整條固定 77（約 30%）。
- 改為單一路徑並把透明度延後到圖層合成，可消除接點與自我重疊的異常加深。

### 驗證

- Skia 像素取樣：舊畫法 min=224、max=247；新畫法 min=max=77
- `dotnet test Clippings.sln --no-restore`：41 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- Debug 建置：0 警告、0 錯誤

### 待辦／已知限制

- 尚未在低效能裝置上量測大量螢光筆圖層的即時預覽效能

---

## 2026-10-31 - v1.5.1

### 目標

修正 1.5.0 開啟編輯器失敗，以及結束程式時再次出現錯誤訊息的回歸。

### 變更

- `EditorWindow.xaml`：移除工具選單在 XAML 載入期間的預設選取
- `EditorWindow.xaml.cs`：影像與復原資源提前建立，初始化完成後才處理工具與尺寸事件
- `EditorWindow.xaml.cs`：建構失敗與視窗關閉時皆能安全且只釋放一次資源
- `EditorWindow.xaml.cs`：程式結束時略過編輯器的未儲存提示與忙碌阻擋
- `HistoryPage.xaml.cs`：編輯器建構失敗時釋放已載入影像
- 版本升至 1.5.1

### 問題與解法

- 日誌顯示 `ImageCodec.Load` 成功，例外發生在 `EditorWindow.InitializeComponent`；工具選單的 `SelectedIndex` 在畫布尚未建立時觸發 `StopPan`，造成 `NullReferenceException`。
- 建構失敗的視窗仍被 WPF 保留，`Application.DoShutdown` 關閉它時再存取尚未建立的 `_undo`，形成第二個錯誤訊息。
- 將事件初始化延後到控制項建立完成，並以 `_initialized` 保護事件；建構失敗與 `Closed` 共用同一套資源釋放流程。

### 驗證

- `dotnet test Clippings.sln --no-restore`：41 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- Debug 建置：0 警告、0 錯誤
- 以暫存 STA 測試程式載入相同資源並實際建立、顯示、關閉 `EditorWindow`，輸出 `EDITOR_INIT_OK`
- 以既有歷史項目開啟編輯器、完成新擷取後進入編輯器，並從系統匣結束，日誌不再出現 `StopPan` 或 `OnWindowClosed` 的 `NullReferenceException`

### 待辦／已知限制

- 尚無自動化 UI 測試涵蓋編輯器 XAML 初始化與關閉流程

---

## 2026-10-31 - v1.5.0

### 目標

整理設定、剪貼簿歷史與編輯器操作，修正圖示、文字、平移及裁切復原問題。

### 變更

- `SettingsPage`：開機自動啟動移至最上方；快捷鍵只能直接錄製新組合鍵取代原值
- `ClipboardPage`：新增並預設「全部」篩選，卡片與捲軸增加 8 DIP 間距
- `EditorWindow`：工具改為單一下拉選單、顏色改為 16 色彈出選單、線寬擴充至 20 px
- `EditorWindow`：新增手形左鍵平移，螢光筆改為 30% 不透明
- `EditorWindow`：文字輸入與 Skia 繪製統一使用 Microsoft JhengHei UI，並正規化換行字元
- `UndoStack`、`EditorWindow`：歷史狀態加入共享影像生命週期，裁切可同步復原影像與註解
- 所有 WPF 視窗明確指定 `Assets/clippings.ico`；版本升至 1.5.0

### 問題與解法

- 原復原紀錄只保存註解，裁切又立即釋放原圖；改以參照計數共享影像，歷史淘汰或清除時才釋放不再使用的 bitmap。
- WPF 輸入框會自動進行中文字型 fallback，但 Skia 原本使用預設字型；改用繁體中文字型並移除每行殘留的 CR 控制字元。
- Windows 工作列固定項目的圖示由 Shell 捷徑與快取管理；程式資源已統一，既有固定項目仍需解除後由新版 EXE 重新固定。
- 標準輸出目錄被執行中的 Clippings 鎖定，改用 `%TEMP%\opencode\clippings-build` 完成不干擾執行中程式的 Debug 建置。

### 驗證

- `dotnet test --no-restore`：41 項通過
- 暫存輸出 Debug build：0 警告、0 錯誤

### 待辦／已知限制

- 工作列已固定的舊圖示需由使用者解除固定後重新固定
- 尚未在混合 DPI 多螢幕環境完成編輯器工具列與平移操作實測

---

## 2026-10-31 - v1.4.0

### 目標

讓夜間主題彼此具有清楚差異，增加一款冷色系淺色主題，並依明暗系列整理主題選單順序。

### 變更

- `Styles/DuskBlue.xaml`：重新設計為海軍藍底與青藍強調色，使用者顯示名稱改為午夜藍
- `Styles/`：新增霧藍晨光、暮紫與墨綠夜完整色彩資源字典
- `AppTheme`、`ThemeService`：加入三個新設定值、資源對應與標題列明暗分類
- `SettingsPage`：主題順序改為跟隨系統、四款淺色、四款深色
- 保留既有 `DuskBlue` enum 名稱與數值，讓已保存的暮夜藍設定自動套用重新設計的午夜藍
- 版本升至 1.4.0，`AssemblyVersion` 與 `FileVersion` 升至 1.4.0.0

### 問題與解法

- 原暮夜藍與中性深色都使用接近的低飽和深灰背景；新午夜藍同步調整背景、表面、邊框、選取與強調色，不只更換按鈕顏色。
- enum 以字串保存於設定檔，直接更名會使既有設定無法反序列化；因此只更改使用者可見名稱與色彩資源。
- 初次標準建置時執行中的 Clippings 鎖定輸出 DLL；先改用暫存 artifacts 目錄完成乾淨建置，程式關閉後再以標準輸出目錄建置成功。

### 驗證

- `dotnet build Clippings.sln`：0 警告、0 錯誤
- `dotnet test Clippings.sln --no-build`：39 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- 八份實際配色字典各有 23 個資源鍵；新增與重繪的四份字典逐鍵比較一致

### 待辦／已知限制

- 新增配色尚未在不同面板類型與夜間環境完成長時間舒適度測試

---

## 2026-10-31 - v1.3.1

### 目標

避免錄製快捷鍵時誤觸擷取功能，並修正設定頁非字串下拉選項沒有顯示文字。

### 變更

- `GlobalHotkeyService`：加入可成對釋放的快捷鍵派送抑制範圍；抑制期間仍消耗 `WM_HOTKEY`，但不執行回呼
- `SettingsPage`：快捷鍵欄位聚焦時抑制全域快捷鍵，離開欄位、頁面卸載、切頁或隱藏主視窗時恢復
- `Styles/Controls.xaml`：`ComboBox` 與 `ComboBoxItem` 改用 `ContentPresenter`，支援整數、`DisplayMemberPath`、`ItemTemplate` 與格式化文字
- 版本升至 1.3.1，`FileVersion` 升至 1.3.1.0；`AssemblyVersion` 維持 1.3.0.0

### 問題與解法

- WPF 的 `PreviewKeyDown` 與 Win32 `WM_HOTKEY` 是獨立事件路徑，將鍵盤事件標記為已處理無法阻止全域快捷鍵。改在全域快捷鍵派送點依錄製狀態略過回呼，且不解除系統註冊。
- 共用下拉模板原本直接將 `Content` 與 `SelectionBoxItem` 指派給 `TextBlock.Text`，只有字串能正常顯示。改用標準內容呈現器保留資料模板與顯示成員設定。

### 驗證

- `dotnet build Clippings.sln`：0 警告、0 錯誤
- `dotnet test Clippings.sln --no-build`：36 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過

### 待辦／已知限制

- 尚未以 UI Automation 驗證所有主題下的下拉選單互動狀態

---

## 2026-10-31 - v1.3.0

### 目標

在既有淺色與深色之外，加入三種適合長時間觀看的柔和配色，並讓使用者選取後立即預覽。

### 變更

- `Styles/`：新增暖色紙張、柔和鼠尾草與暮夜藍完整色彩資源字典
- `ThemeService`、`App.xaml.cs`：依主題載入資源並同步所有視窗的原生標題列明暗狀態
- `SettingsPage`：主題選項改為型別化資料，選取後立即預覽；儲存前不寫入設定，放棄變更時還原已儲存主題
- `AppStrings.cs`：集中六種主題的顯示名稱
- `AppSettingsTests.cs`：固定既有 enum 數值並驗證三種新主題可正確複製
- 版本升至 1.3.0，`AssemblyVersion` 與 `FileVersion` 升至 1.3.0.0

### 問題與解法

- 原本設定頁直接以 `ComboBox` 索引對應 enum，增加選項容易產生錯配；改用含設定值與顯示名稱的型別化選項。
- 即時預覽不能在選取時直接保存；由共用主題套用入口更新畫面，仍只在按下儲存時寫入 `settings.json`，重新載入設定則恢復已保存主題。

### 驗證

- `dotnet build Clippings.sln`：0 警告、0 錯誤
- `dotnet test Clippings.sln --no-build`：36 項通過
- `dotnet format Clippings.sln --verify-no-changes --no-restore`：通過
- Light、Dark、WarmPaper、SoftSage、DuskBlue 五份主題字典各有 23 個資源鍵，逐鍵比較一致

### 待辦／已知限制

- 混合 DPI 多螢幕環境尚未實測
- 三款新配色尚未在實際顯示器完成長時間視覺舒適度測試

---

## 2026-10-31 - v1.2.6

### 目標

讓系統匣右鍵選單只跟隨 Windows 淺色／深色模式，不受 Clippings AP 內主題切換影響。

### 變更

- `App.xaml.cs`：建立托盤選單時，在 `ContextMenu.Resources` 覆寫 `SurfaceBrush`、`BorderBrush`、`TextPrimaryBrush`、`TextSecondaryBrush`、`HoverBrush`
- 托盤選單的 `Opened` 事件會重新呼叫 `ThemeService.IsSystemDark()`，依 Windows 當前模式更新本機筆刷
- TextBox 與其他 AP 內選單仍使用 AP 主題資源，不受托盤選單本機資源影響
- 版本升至 1.2.6，`FileVersion` 升至 1.2.6.0；`AssemblyVersion` 維持 1.2.0.0

### 問題與解法

- 托盤選單原本直接解析 AP 的全域主題筆刷，因此切換 AP 主題會改變托盤選單。改用 `ContextMenu` 本機同名資源覆寫，讓既有模板優先解析 Windows 專用色彩。
- 初次建置時條件運算式的十六進位常值被推斷為 `int`，無法傳入 `byte` 參數；改由筆刷輔助函式集中轉型。
- 後續建置因執行中的 Clippings 鎖定 `Clippings.exe`；結束該程序後重新建置成功。

### 驗證

- `dotnet build`：0 警告、0 錯誤
- `dotnet test --no-build`：32 項通過
- `dotnet format --verify-no-changes`：通過
- Windows 目前為淺色模式；以反射直接呼叫實際托盤主題方法，確認本機資源為 `Surface #F9F9F9`、`Border #DADADA`、`Text #1A1A1A`、`Secondary #616161`、`Hover #E5E5E5`
- 將 AP 暫時設為深色後啟動，Windows 主題判定仍為淺色；測試後設定已還原為 `System`
- Windows 通知區仍未將 H.NotifyIcon 右鍵 Popup 項目暴露給 UI Automation，無法直接擷取實際托盤選單項目

### 待辦／已知限制

- 使用 Windows 淺色／深色模式，不以 Windows 強調色作為選單背景

---

## 2026-10-31 - v1.2.5

### 目標

修正深色模式下系統匣右鍵選單滑鼠懸停時出現黑底黑字，導致項目文字無法辨識。

### 變更

- `Styles/Controls.xaml`：`AppMenuItemStyle` 的標題與快捷鍵文字改為命名元素，直接使用主題文字筆刷
- `IsHighlighted` 與啟用狀態的 `IsMouseOver` 觸發器明確設定背景、標題與快捷鍵文字色
- 版本升至 1.2.5，`FileVersion` 升至 1.2.5.0；`AssemblyVersion` 維持 1.2.0.0

### 問題與解法

- 托盤選單由 H.NotifyIcon 的 Popup 顯示，標題原本透過 `TemplateBinding Foreground` 取得顏色；滑鼠反白時可能繼承系統黑色前景，但背景已切換成深色 `HoverBrush`。改為由模板內文字元素直接解析 `TextPrimaryBrush`／`TextSecondaryBrush`，並在反白與懸停觸發器再次指定，避免 Popup 的系統前景覆蓋。

### 驗證

- `dotnet build`：0 警告、0 錯誤
- `dotnet test --no-build`：32 項通過
- `dotnet format --verify-no-changes`：通過
- 深色主題 UI Automation：共用 `AppMenuItemStyle` 的懸停背景為 `47,53,65`（`HoverBrush`），項目內偵測到 88 個亮色文字像素，不再是黑底黑字
- 自動化可定位 Clippings 的隱藏系統匣圖示；Windows 通知區未將 H.NotifyIcon 右鍵 Popup 項目暴露給 UI Automation，因此以托盤與 TextBox 共用的同一個 MenuItem 模板完成像素檢查

### 待辦／已知限制

- 混合 DPI 多螢幕尚未實測

---

## 2026-10-31 - v1.2.4

### 目標

移除右鍵選單前方的圖示欄色塊，修正深色模式選單文字不可見，並將圖示重畫為清楚的向量相片堆疊。

### 變更

- `Styles/Logo.xaml`：新增 `ClippingsMarkImage` 向量 `DrawingImage`（藍底、淺藍與白色相片卡、藍色山形、黃色太陽）
- `App.xaml`：合併 `Styles/Logo.xaml`
- `Views/MainWindow.xaml`：左上角改用 `ClippingsMarkImage`，不再使用點陣 PNG
- `Assets/clippings.ico`：重新產生 16／24／32／48／64／128／256 多尺寸 ICO
- `Assets/clippings-logo.png`：移除
- `Clippings.App.csproj`：移除 PNG Resource
- `Styles/Controls.xaml`
  - `AppContextMenuStyle` 加入自訂 `ContextMenu` 模板，固定深色／淺色背景並移除預設選單外框
  - `AppMenuItemStyle` 與 `AppSeparatorStyle` 改為 keyed 資源，並保留隱式 `BasedOn`
- `App.xaml.cs`：托盤選單建立時明確套用 keyed 樣式；啟動時註冊 `TextBoxContextMenuHelper`
- `Helpers/TextBoxContextMenuHelper.cs`：為所有 TextBox 建立或套用樣式化右鍵選單（復原／剪下／複製／貼上／全選）
- `Strings/AppStrings.cs`：新增復原、剪下、貼上、全選字串
- 版本升至 1.2.4，`FileVersion` 升至 1.2.4.0；`AssemblyVersion` 維持 1.2.0.0

### 問題與解法

- **選項前方色塊**：像素掃描確認是預設 `MenuItem` 的圖示欄（約 34 px），且系統建立的 TextBox 右鍵選單沒有套用到隱式樣式。改為 keyed 樣式，並在托盤選單建立與 TextBox 載入時明確套用；TextBox 改用自訂的標準命令選單。
- **深色模式文字不可見**：預設 `ContextMenu` 模板使用系統淺色底，搭配自訂 MenuItem 的淺色文字就會看不見。改為自訂 `ContextMenu` 模板，背景固定使用 `SurfaceBrush`，文字使用 `TextPrimaryBrush`。
- **圖示模糊**：舊 ICO 的 256 影格是 64 影格放大，沒有更多細節。重新以向量繪製相片堆疊，主視窗直接使用 `DrawingImage`；ICO 由同一組幾何產生多尺寸 DIB 影格，避免放大模糊。
- **ICO 與 System.Drawing 相容性**：使用 32bpp DIB 影格（含 AND mask）而非 PNG 壓縮影格；`System.Drawing.Icon` 可讀取 16／32／64／128 影格，WPF 可讀取到 256 影格。

### 驗證

- `dotnet build`：0 警告、0 錯誤
- `dotnet test`：32 項通過
- `dotnet format --verify-no-changes`：通過
- UI Automation：
  - 深色主題右鍵選單「貼上」項目：左側 16 px 為背景色 `35,38,46`，不再有白色圖示欄；文字為亮色
  - 淺色主題右鍵選單可正常顯示與操作
  - 主視窗左上角圖示為 40×40 px（125% DPI），來源為向量
  - 新 ICO 影格：16／24／32／48／64／128／256；`System.Drawing.Icon` 可載入 16／32／64／128
  - 應用程式啟動與選單開啟正常
- 尚未實測：托盤圖示右鍵選單的直接視覺檢查、200% DPI、混合 DPI 多螢幕

### 待辦／已知限制

- TextBox 右鍵選單改為自訂標準命令，不再包含系統額外的輸入法或拼字建議項目
- 托盤選單與 TextBox 選單共用同一組樣式，但托盤選單尚未以自動化直接開啟驗證
- 圖示設計可在 `Styles/Logo.xaml` 與 ICO 產生流程中再微調

---

## 2026-10-31 - v1.2.3

### 目標

修正圖示清晰度、深色主題選單可讀性、快捷鍵錄製、搜尋游標、設定離開提示與關閉主視窗提示。

### 變更

- `Assets/clippings-logo.png`：改由 ICO 原生 64×64 影格匯出
- `Views/MainWindow.xaml`：圖示改為 32×32 DIP，加入 `UseLayoutRounding`、`SnapsToDevicePixels`
- `Styles/Controls.xaml`
  - `BaseButtonStyle` 已在前一版改為明確文字前景；本版補齊 `ComboBox` 完整範本
  - 新增 `ComboBoxItem`、`ContextMenu`、`MenuItem`、`Separator`、`ToolTip` 深色主題樣式
- `Views/SettingsPage.xaml(.cs)`
  - 快捷鍵欄位加入 `PreviewKeyDown` 錄製，處理修飾鍵、Esc 清除、`KeyInterop`
  - 設定 dirty 改為 `SettingsSnapshot` 值比較；新增 `HasChanges`、`CaptureSnapshot`
- `Views/HistoryPage.xaml(.cs)`、`ClipboardPage.xaml(.cs)`：搜尋提示在聚焦時隱藏，空白時游標歸零
- `Views/MainWindow.xaml.cs`：移除關閉時的系統匣提示、`_trayHintShown` 與 `INotificationService` 依賴
- `Strings/AppStrings.cs`：移除未使用的 `TrayStillRunning`
- 版本升至 1.2.3，`FileVersion` 升至 1.2.3.0；`AssemblyVersion` 維持 1.2.0.0

### 問題與解法

- **圖示仍模糊**：分析後發現 ICO 的 256×256 影格是 64×64 的放大版，縮回 64 與原生 64 的 MAE 只有 0.45；256 沒有更多細節。改用原生 64 PNG，並把顯示尺寸由 34 DIP 改為 32 DIP，讓 100%／125%／150%／200% 分別對應 32／40／48／64 px。
- **深色主題文字看不到**：WPF 預設 ComboBox／ContextMenu／MenuItem 使用系統淺色底，而全域 TextBlock 在深色主題給淺色字。ComboBox 收合狀態也需要自訂範本才能顯示選取值；改用完整 ComboBox 範本與深色選單樣式。
- **快捷鍵錄製**：以 `KeyInterop.VirtualKeyFromKey` 轉換 WPF `Key`，組合鍵直接寫入欄位；沒有修飾鍵的一般字元仍保留手動輸入。
- **搜尋游標位置**：提示文字在聚焦時仍顯示，且起點比 TextBox 內容區略左，造成游標視覺上落在「搜」「尋」之間。改為聚焦即隱藏提示，空白時 `CaretIndex=0`。
- **設定誤判為已修改**：原本用事件旗標，載入或控制項延遲事件可能誤標。改為與載入時的快照做值比較，熱鍵以 `DisplayName` 正規化後比較。
- **關閉提示**：移除 `MainWindow.OnClosing` 的通知與相關欄位；關閉仍會隱藏到系統匣。

### 驗證

- `dotnet build`：0 警告、0 錯誤
- `dotnet test`：32 項通過
- `dotnet format --verify-no-changes`：通過
- UI Automation：
  - 點擊 `RegionHotkeyBox` 後送出 Ctrl+Shift+Y，欄位值為 `Ctrl+Shift+Y`
  - 設定頁未變更直接離開、切換 checkbox 兩次後離開，皆未出現儲存詢問
  - 深色主題：ComboBox 下拉選項亮暗像素比正常、設定頁可見的 DelayCombo 有亮色文字、TextBox 右鍵選單貼上有亮色文字
  - 淺色主題：DelayCombo 為淺底深字，選取 `10` 可正常更新
  - 搜尋框聚焦後提示文字不再出現在自動化樹中
  - 主視窗關閉後程序仍在系統匣，視窗隱藏且無提示視窗
  - 圖示資產為 64×64 PNG，125% DPI 下顯示為 40×40 px

### 待辦／已知限制

- 圖示原始素材實際只到 64 px；若仍要求更銳利，需要 512 px 以上 PNG 或 SVG
- 尚未實測 200% DPI、混合 DPI 多螢幕與托盤選單的直接視覺檢查（托盤與文字右鍵選單共用同一組 ContextMenu/MenuItem 樣式）
- 快捷鍵仍要求至少一個修飾鍵；尚未支援 F1～F24 單鍵錄製

---

## 2026-10-31 - v1.2.2

### 目標

修正主視窗圖示模糊、按鈕文字對比、快速擷取按鈕截斷、框選操作列位置漂移、清單滾輪失效，並為剪貼簿歷史加入類型切換。

### 變更

- `Assets/clippings-logo.png`：由 ICO 的 256×256 影格匯出；`Clippings.App.csproj` 加入 Resource
- `Views/MainWindow.xaml`：左上角改用 PNG 並啟用 `HighQuality` 縮放；快速擷取按鈕由 `UniformGrid` 改為 `WrapPanel`
- `Styles/Controls.xaml`
  - `BaseButtonStyle` 改用明確 `TextBlock` 綁定 `Foreground`，避免全域 `TextBlock` 樣式覆蓋按鈕前景
  - `PrimaryButtonStyle` 改用專用 `PrimaryButtonBrush` 系列
- `Styles/Light.xaml`、`Dark.xaml`：新增主要按鈕背景／hover／pressed 資源；深色主題按鈕文字改為白色
- `Views/CaptureOverlayWindow.xaml.cs`：新增 `MeasureContentSize`，扣除 Margin 後再計算操作列、尺寸標籤與放大鏡位置
- `Views/HistoryPage.xaml`、`ClipboardPage.xaml`：移除外層 `ScrollViewer`，改由 `ListBox` 使用像素捲動
- `Views/ClipboardPage.xaml(.cs)`：新增圖片／文字／檔案三段切換，預設圖片；切換時清空搜尋並重新載入
- `Core/Services/IClippingsStore.cs`、`Platform/Storage/ClippingsStore.cs`：查詢加入 `ClipboardItemKind? kind` 篩選
- `Strings/AppStrings.cs`：新增各類型空狀態文字
- 版本升至 1.2.2，`FileVersion` 升至 1.2.2.0；`AssemblyVersion` 維持 1.2.0.0

### 問題與解法

- **ICO 圖示模糊**：ICO 內雖有 256×256 影格，但 WPF 以 ICO 當 `Image.Source` 時取到較小影格再放大。改為先匯出 PNG，並用 `HighQuality` 縮放。
- **藍底黑字**：全域隱式 `TextBlock` 樣式會覆蓋按鈕的 `Foreground`。改由按鈕模板直接綁定 `TextBlock.Foreground`，並使用專用主要按鈕色票。
- **按鈕文字截斷**：三欄 `UniformGrid` 每欄約 60 DIP，容不下「目前螢幕」。改為自動寬度的 `WrapPanel`。
- **操作列位置漂移**：`FrameworkElement.DesiredSize` 包含 `Margin`，而先前的位置仍留在 `Margin` 中，導致每次量測都把舊位移加進去。改為量測後扣除目前 Margin。
- **滾輪失效**：外層 `ScrollViewer` 包住內層 `ListBox`，內層停用垂直捲動但仍攔截滾輪。改由 `ListBox` 自己捲動，並設定 `CanContentScroll=False`。
- **剪貼簿篩選**：查詢層直接以 `ClipboardItemKind` 過濾，避免只在前端篩選造成筆數與空狀態不一致。

### 驗證

- `dotnet build`：0 警告、0 錯誤
- `dotnet test`：32 項通過
- `dotnet format --verify-no-changes`：通過
- UI Automation 實測：
  - 主要按鈕區域取樣（淺色主題）：白色文字像素 67、黑色像素 0
  - 「目前螢幕」按鈕實測寬度 86 px（約 69 DIP），文字不再截斷
  - 同一框選範圍重複兩次，操作列位置差異為 (0, 0)
  - 滑鼠滾輪在最近擷取清單可從 0% 捲動到 21.4%
  - 剪貼簿頁可切換圖片／文字／檔案，空狀態文字正確更新
- 尚未實測：200% DPI、混合 DPI 多螢幕、觸控板手勢、深色主題實機視覺檢查

### 待辦／已知限制

- 圖片類型目前不支援文字搜尋，切換到圖片時會清空搜尋字串
- 最近擷取仍使用 `WrapPanel`，未虛擬化；大量項目時的效能可再評估
- 混合 DPI 多螢幕與跨螢幕框選仍待實機確認

---

## 2026-10-31 - v1.2.1

### 目標

在不改變功能的前提下，降低擷取與編輯大圖時的記憶體、UI 卡頓與長期資料量，並整理幾個高耦合點。

### 變更

- `Platform/Capture/ScreenCaptureService.cs`：以 `SKBitmap.InstallPixels` 直接包住 GDI DIB，BitBlt 直接寫入像素；release delegate 負責 `DeleteObject`；新增可傳入螢幕清單的 `CaptureVirtualScreen` 多載
- `App/Services/CaptureCoordinator.cs`
  - 擷取、裁切、縮圖／PNG 編碼、SHA-256 移出 UI 執行緒
  - 全螢幕擷取不再額外複製整張影像；自動存檔與另存改用非同步寫入
  - 多螢幕 overlay 先在背景裁切並建立 `BitmapSource`，建立後提早釋放整張虛擬螢幕
  - 單次擷取只列舉一次螢幕清單
- `App/Helpers/BitmapSourceHelper.cs`：`BitmapSource.Create` 與 `CopyPixels` 改用 `IntPtr`，移除全影像 `byte[]`
- `Platform/Storage/ClippingsStore.cs`
  - 快取 `DbContextOptions`、連線加 `Default Timeout=5`
  - `Initialize` 啟用 WAL 並以 `CREATE INDEX IF NOT EXISTS` 補複合索引
  - 歷史／剪貼簿 Trim 改為只投影 `Id/FilePath/ThumbnailPath`，再用 `ExecuteDeleteAsync`
  - 新增 `CountClipboardItemsAsync`，剪貼簿超過上限加緩衝才 Trim
  - 所有非同步方法補 `ConfigureAwait(false)`
- `Platform/Storage/ClippingsDbContext.cs`：加入收藏／釘選與 `CreatedAt` 的複合索引
- `Core/Services/IClippingsStore.cs`：介面加入 `DataRoot` 與 `CountClipboardItemsAsync`
- `Platform/Imaging/ImageCodec.cs`：新增 `EncodeThumbnailJpeg`，縮圖統一轉 JPEG
- `App/Services/ClipboardWatcher.cs`：縮圖改 JPEG；影像處理失敗寫入日誌
- `App/Views/HistoryPage.xaml.cs`、`ClipboardPage.xaml.cs`：查詢結果直接指派 `ItemsSource`，不再逐筆 `ObservableCollection.Add`；補刪除／釘選／清除例外處理
- `App/Helpers/PathToImageConverter.cs`、兩個清單 XAML：可依 `ConverterParameter` 指定解碼寬度（歷史 250、剪貼簿 96）
- `App/Views/EditorWindow.xaml(.cs)`：複製／儲存改非同步並在忙碌時停用畫布；無註解時直接輸出原圖；筆跡改增量累積；復原／重做會釋放不再使用的馬賽克快取
- `App/Logging/FileLoggerProvider.cs`：改用 `Channel` 背景寫入，啟動時清除 7 天前日誌
- `App/Services/INotificationService.cs`、`TrayNotificationService.cs`：通知改由注入服務處理；`MainWindow` 一併注入 `CaptureCoordinator`
- 版本升至 1.2.1，`FileVersion` 升至 1.2.1.0；`AssemblyVersion` 維持 1.2.0.0

### 問題與解法

- **`Marshal.Copy` 沒有 IntPtr 對 IntPtr 的多載**：原本想直接複製兩塊原生記憶體，確認 API 後改用 `SKBitmap.InstallPixels` 讓 BitBlt 直接寫入 SKBitmap 的像素，反而少一次複製。DIB 必須在最後才刪除，因此用 `SKBitmapReleaseDelegate` 綁定 `DeleteObject`。
- **SKCanvas 舊多載觸發警告**：`DrawBitmap(SKBitmap, float, float)` 已過時，改用帶 `SKSamplingOptions` 的 `DrawBitmap` 多載。
- **通知服務不應暴露 TaskbarIcon**：`INotificationService` 只保留 `Show`；App 另外解析具體 `TrayNotificationService` 來初始化與清除 tray 參照。
- **SQLite 連線集區會鎖住測試資料庫**：煙霧測試刪除暫存目錄時遇到 `clippings.db` 被占用，呼叫 `SqliteConnection.ClearAllPools()` 後可正常刪除；應用程式本身不需要額外處理。
- **WAL 會產生 `-wal`／`-shm` 檔**：屬 SQLite 正常行為，已確認資料庫可正常建立、刪除與清理。

### 驗證

- `dotnet build`：0 警告、0 錯誤
- `dotnet test`：32 項通過
- `dotnet format --verify-no-changes`：通過
- 暫時性煙霧測試：
  - 擷取 1536×864，PNG 解碼後 alpha 全為 255
  - 縮圖 JPEG 320×180，可正常解碼
  - 擷取 Trim 後 DB 筆數歸零、原圖與縮圖皆刪除
  - 剪貼簿 Trim 後筆數歸零
  - 資料夾出現 `clippings.db-wal`／`clippings.db-shm`
- 應用程式啟動煙霧測試通過；以 UI Automation 觸發全螢幕擷取（1920×1080）成功，日誌無錯誤
- 區域框選 overlay 開啟並取消，日誌無「擷取流程失敗」

### 待辦／已知限制

- 混合 DPI 多螢幕、跨螢幕框選仍待實機確認
- 剪貼簿影像含透明區域時，縮圖會以白底呈現（僅影響預覽）
- 編輯器超長筆畫與 4K 連續複製／儲存仍建議實機壓力測試
- WGC 視窗擷取、滾動長截圖、螢幕錄影仍未實作
- 目前介面文字仍集中於 `AppStrings`，尚未改為 `.resx`

---

## 2026-10-31 - v1.2.0

### 目標

讓框選完成後可以移動選取範圍並調整大小，減少重複重新框選。

### 變更

- `Core/Editing/ResizeEdges.cs`：新增縮放邊旗標型別
- `Core/Editing/PixelRect.cs`：新增 `MoveWithin` 與 `ResizeWithin`，移動保持寬高、縮放限制於影像邊界與最小尺寸
- `Views/CaptureOverlayWindow.xaml`：恢復控制點圖層、加入 `LostMouseCapture`、操作列游標與更新提示文案
- `Views/CaptureOverlayWindow.xaml.cs`
  - 新增 Drawing／Selected／Moving／Resizing 的互動狀態與八方向命中測試
  - 框內拖曳移動、邊與角拖曳縮放，游標依方向切換
  - 以操作開始矩形加總像素位移，避免逐幀累積誤差
  - 操作中隱藏操作列與放大鏡；放開後重新定位
  - Esc／右鍵／失去滑鼠捕捉會恢復操作前範圍
- `tests/Clippings.Core.Tests/PixelRectTests.cs`：新增移動邊界與八方向縮放測試
- 版本升至 1.2.0，組件版本同步升至 1.2.0.0

### 問題與解法

- **高 DPI 縮放漂移**：若每幀從上一幀累加，125%／150% 會累積捨入誤差；改為固定以操作開始矩形加總 DIP 位移並四捨五入為像素。
- **小範圍命中歧義**：左右或上下命中帶重疊時，改為選擇距離較近的邊，距離相同才固定優先左／上，避免游標閃爍。
- **拖曳卡住**：新增 `LostMouseCapture` 處理，意外失去捕捉時取消本次調整並恢復原矩形。
- **控制點誤導**：控制點只作視覺提示並設為 `IsHitTestVisible=False`，命中統一由視窗幾何判斷，避免事件分裂。

### 驗證

- `dotnet build`：0 警告、0 錯誤
- `dotnet test`：32 項通過
- `dotnet format --verify-no-changes`：通過
- 啟動煙霧測試通過
- 多 DPI、混合 DPI 多螢幕與各主題的實際拖曳操作待實機確認

### 待辦／已知限制

- 選取範圍仍限制在單一螢幕，尚未支援跨螢幕移動或縮放
- 縮放拖過相對邊時停在最小尺寸，不支援方向翻轉
- 尚未支援方向鍵微調

---

## 2026-10-31 - v1.1.0

### 目標

完成背景操作回饋、輸出設定可信度、主功能可發現性與常用頁面的低風險介面改善。

### 變更

- `App.xaml.cs`：集中顯示系統匣通知；快捷鍵失敗同步至主畫面；剪貼簿歷史寫入失敗時通知
- `Services/CaptureCoordinator.cs`：檢查複製結果、通知歷史與自動存檔失敗；自動存檔及另存套用預設格式與 JPEG 品質
- `Views/MainWindow.xaml(.cs)`：提供四種擷取入口、快捷鍵摘要與失敗狀態、Ctrl+F、首次關閉系統匣提示
- `Views/SettingsPage.xaml(.cs)`：固定儲存列、dirty 追蹤、離頁確認、JPEG 品質設定與欄位驗證焦點
- `Views/HistoryPage.xaml(.cs)`：加入載入、錯誤、重試與搜尋提示／清除
- `Helpers/ClipboardHelper.cs`、`Views/ClipboardPage.xaml(.cs)`：還原 FileDrop、搜尋提示／清除，查詢筆數改用設定值
- `Services/ClipboardWatcher.cs`：文字與檔案雜湊加入類型前綴，避免內容相同時互相去重
- `Styles/Controls.xaml`、`Views/EditorWindow.xaml(.cs)`：色票改為可持續選取的單選控制項，線寬顯示單位；另存套用格式與品質
- 版本升至 1.1.0，組件版本同步升至 1.1.0.0

### 問題與解法

- **背景操作無聲失敗**：由 `App` 統一封裝系統匣通知，詳細例外保留於日誌，使用者只看到可理解的結果。
- **輸出設定未生效**：歷史庫繼續保存 PNG，自動輸出與另存則依設定編碼，避免破壞既有無損歷史。
- **設定容易遺失**：集中監聽欄位變更，固定儲存操作並在離頁、隱藏主視窗前詢問。
- **檔案歷史只能複製路徑**：沿用既有換行路徑資料，還原時建立 Windows FileDrop，不需資料庫遷移。
- **頁面狀態與選取不明確**：加入互斥載入／錯誤／空狀態，以及具持續外框的色票選取狀態。

### 驗證

- `dotnet format --verify-no-changes`：通過
- `dotnet build`：0 警告、0 錯誤
- `dotnet test`：21 項通過
- Debug 執行檔啟動後持續運作 4 秒，WPF 啟動煙霧測試通過；測試後已結束程序

### 待辦／已知限制

- Windows 專注輔助或通知設定可能阻擋系統匣通知
- 檔案剪貼簿只保留來源路徑，來源移動或刪除後無法還原
- WPF 對話框、系統匣通知與各主題畫面仍需實機互動確認

---

## 2026-10-31 - v1.0.6

### 目標

以低風險修改改善輸出正確性、編輯資料安全、擷取流程與介面語意一致性。

### 變更

- `Views/HistoryPage.xaml.cs`：依另存格式輸出正確副檔名，JPEG 會載入原圖並重新編碼
- `Views/EditorWindow.xaml.cs`：裁切後標記未儲存；複製到剪貼簿不再清除未儲存狀態
- `Services/CaptureCoordinator.cs`：所有成功擷取都加入歷史；從可見主視窗啟動且取消或失敗時恢復主視窗
- `Views/ClipboardPage.xaml`：清除按鈕改為「清除未釘選」，釘選按鈕依狀態顯示「釘選／取消釘選」
- `Views/CaptureOverlayWindow.xaml(.cs)`：移除不可拖曳的四角控制點
- 版本升至 1.0.6

### 問題與解法

- **JPEG 副檔名與內容不符**：原本不論格式都直接複製歷史 PNG；現在選擇 JPEG 時使用 `ImageCodec` 真正重新編碼。
- **編輯內容可能無提示遺失**：複製不是持久化儲存，不再清除 `IsDirty`；裁切會改變影像，因此完成後設為未儲存。
- **編輯擷取未進歷史**：移除 Edit 動作的歷史排除條件，先保存原始擷取再開啟編輯器。
- **取消擷取後主視窗消失**：記錄擷取前主視窗可見狀態，只在原本可見且流程未成功時恢復。
- **介面暗示與行為不一致**：修正文案並移除尚未支援拖曳的控制點。

### 驗證

- `dotnet build`：0 警告、0 錯誤
- `dotnet test`：21 項通過
- `dotnet format --verify-no-changes`：通過
- 已確認舊的 `HandleLayer`、`AddHandle` 與 `HandleFill` 無殘留參照
- JPEG 檔案辨識、取消擷取恢復視窗及編輯器關閉提示待實機確認

### 待辦／已知限制

- JPEG 透明像素會依編碼器規則處理；一般螢幕擷取來源為不透明影像
- 編輯器裁切仍會清除既有復原堆疊

---

## 2026-10-31 - v1.0.5

### 目標

修正框選完成後，浮動操作列呈現黑色且看不到按鈕與文字的問題。

### 變更

- `Styles/Controls.xaml`：操作列按鈕模板改為明確繪製文字，並綁定按鈕的前景、字型與字級
- 版本升至 1.0.5

### 問題與解法

- **操作列黑底黑字**：工具列按鈕雖已設定淺色 `Foreground`，自訂模板內的內容未明確使用該前景色，可能被全域 `TextBlock` 樣式覆蓋；改用具本地 `Foreground` 綁定的 `TextBlock`，確保文字使用 `ToolbarTextBrush`。

### 驗證

- `dotnet build`：0 警告、0 錯誤
- `dotnet test`：21 項通過
- `dotnet format --verify-no-changes`：通過
- 淺色與深色主題下的實際操作列顯示待實機確認

### 待辦／已知限制

- 若特定顯示卡仍將整個操作列渲染成黑色，需進一步停用 `DropShadowEffect` 驗證 WPF 陰影中介表面

---

## 2026-10-31 - v1.0.4

### 目標

修正開始拖曳擷取範圍後，游標右下方出現大片黑色區域的問題。

### 變更

- `Views/CaptureOverlayWindow.xaml`：將尺寸標籤固定為左上對齊，使其依內容大小排列，不再延伸至視窗右下角
- 版本升至 1.0.4

### 問題與解法

- **尺寸標籤背景遮住畫面**：`SizeLabel` 位於 `Grid` 中但未指定對齊方式，預設 `Stretch` 使深色背景從標籤位置鋪滿剩餘區域；加入 `HorizontalAlignment="Left"` 與 `VerticalAlignment="Top"` 後，背景只包覆尺寸文字。
- **Debug 輸出被執行中的程式鎖定**：第一次建置時 `Clippings` 鎖住輸出 DLL；保留執行中的程式，改用獨立暫存輸出目錄完成驗證。

### 驗證

- `dotnet build`：0 警告、0 錯誤
- `dotnet test`：21 項通過
- `dotnet format --verify-no-changes`：通過

### 待辦／已知限制

- 混合 DPI 多螢幕仍待實測

---

## 2026-10-31 - v1.0.3

### 目標

修正框選時「選取框內部整片變黑、看不到擷取畫面」的問題。

### 變更

- `Views/CaptureOverlayWindow.xaml`
  - 移除 `DimPath`（EvenOdd 挖洞），改為 `DimLayer` 內的四塊獨立矩形：`DimTop`、`DimBottom`、`DimLeft`、`DimRight`
  - 為所有圖層設定明確 `Panel.ZIndex`：凍結畫面 0、暗化 10、選取框 20、控制點 21、尺寸標籤 30、放大鏡 40、工具列 50、提示列 60、游標 70
- `Views/CaptureOverlayWindow.xaml.cs`
  - `UpdateDim()` 改為依選取框 DIP 座標計算四塊矩形；選取框內完全沒有覆蓋元素
  - 無選取時整個暗化層隱藏；拖曳中即時更新四塊矩形
  - 放大鏡除了游標進入時隱藏，**與選取框相交時也隱藏**
- 版本升至 1.0.3

### 問題與解法

- **選取框內變黑**：原本用單一 `Path` + `EvenOdd` 幾何挖洞，只要路徑或 DPI 對位有誤，洞的位置就可能不對，露出視窗黑色背景。改為四塊獨立矩形後，選取框內不放任何元素，直接顯示凍結畫面。
- **放大鏡可能壓住選取內容**：新增與選取框的相交判斷。

### 驗證

- `RenderTargetBitmap` 視覺樹測試，於 100%／125%／150% 三種縮放：
  - 框選前暗化層為 `Collapsed`
  - 框選中：框內亮度 180 → 180（不變）、框外 180 → 108（約 60%）
  - 放大鏡位於右上角、內容非全黑、與選取框相交時隱藏
  - 三種縮放皆 PASS
- `dotnet build` 0 警告 0 錯誤、`dotnet test` 21 項通過、格式驗證通過
- 實機框選 440×340 並按 Enter 存檔成功，日誌無錯誤

### 待辦／已知限制

- 混合 DPI 多螢幕仍待實測
- 若存出的 PNG 在該區域也是黑的，代表擷取來源本身為黑（硬體加速／受保護內容），需改用 `Windows.Graphics.Capture` 備援

---

## 2026-10-31 - v1.0.2

### 目標

修正框選時畫面過暗、按下滑鼠後幾乎全黑的問題，並讓放大鏡不再遮擋游標。

### 變更

- `Views/CaptureOverlayWindow.xaml.cs`
  - 尚未選取時不再顯示暗化層（`DimPath` 直接 `Collapsed` 並清空 `Data`）
  - 拖曳中就套用「選取框外暗化」的挖洞效果，框內全程保持原亮度
  - 按下左鍵即隱藏頂端提示列
  - 放大鏡改用 `CroppedBitmap` + `Image`（移除 `ImageBrush` 絕對 `Viewbox`）
  - 放大鏡固定在右上角、拖曳超過 6 DIP 才顯示；游標進入面板區域時暫時隱藏
- `Styles/Light.xaml`／`Dark.xaml`：`OverlayDimBrush` 降為 40%／50%
- `Views/CaptureOverlayWindow.xaml`：提示列圓角 999 → 16
- 版本升至 1.0.2

### 問題與解法

- **畫面一開始灰暗、點擊後更暗**：覆蓋層一開啟就套用全螢幕暗化，且框選中沒有挖洞，整片維持暗化。改為「未選取不暗化、框選中只暗化框外」。
- **放大鏡疑似黑色區塊**：`ImageBrush` 搭配絕對 `Viewbox` 可能取樣失敗；改用 `CroppedBitmap`，並移到固定右上角避免遮住選取內容。
- **提示列可能遮住內容**：按下左鍵立即隱藏，並修正過大的圓角值。

### 驗證

- 以 `RenderTargetBitmap` 直接渲染覆蓋層視覺樹（800×600、120 DPI）：
  - 框選前：無暗化（`DimPath` 為 Collapsed）
  - 框選中：框內亮度不變（差 < 6），框外亮度約為原本 60%
  - 放大鏡：`CroppedBitmap`、右上角、亮度變異 > 20（非全黑）
- `dotnet build` 0 警告 0 錯誤、`dotnet test` 21 項通過、格式驗證通過

### 待辦／已知限制

- 混合 DPI 多螢幕仍待實測
- 放大鏡固定右上角，若選取區域正好在該角落，面板會暫時隱藏（避免遮擋游標）

---

## 2026-10-31 - v1.0.1

### 目標

修正擷取覆蓋層全黑、放大鏡位置跳動、游標不易辨識三個問題。

### 變更

- `Helpers/BitmapSourceHelper.cs`：依 `SKAlphaType` 選用 `Bgr32`／`Pbgra32`／`Bgra32`
- `Capture/ScreenCaptureService.cs`：BitBlt 後將 alpha 補為 `0xFF`
- `Views/CaptureOverlayWindow.xaml`：`Cursor=None`、自繪十字游標（40 DIP、白色 6px 光暈 + 藍色 2.5px 內線、中心留空隙）、上方快捷鍵提示列、工具列陰影
- `Views/CaptureOverlayWindow.xaml.cs`：放大鏡只在框選期間顯示；依起始象限固定方位並夾住邊界；尺寸只量測一次；移除重複更新；工具列點擊不再觸發新框選
- 版本升至 1.0.1

### 問題與解法

- **覆蓋層全黑**：`BitBlt` 寫入 32bpp DIB 時不設定 alpha，像素 alpha 全為 0；`BitmapSource` 以 `Bgra32` 呈現時被視為完全透明，露出視窗黑底。改為 `Opaque → Bgr32`，並在擷取時補 alpha。
- **放大鏡亂跳**：`MouseMove` 無條件顯示放大鏡、每次重新 `Measure`，且靠近邊界時左右／上下翻轉。改為僅框選時顯示、固定方位、快取尺寸、邊界夾住。
- **游標太小**：系統 `Cursors.Cross` 無法放大加粗；改自繪十字並隱藏系統游標。
- **工具列誤觸**：點擊浮動工具列時可能同時開始新框選；加入 `ActionBar.IsMouseOver` 判斷。

### 驗證

- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：21 項通過
- `dotnet format --verify-no-changes`：通過
- 實機擷取 442×340：PNG 與剪貼簿影像 alpha 皆為 255，非黑像素 24.7%，檔案 44 KB（修正前同類型擷取僅約 1.2 KB，呈全黑）

### 待辦／已知限制

- 混合 DPI 多螢幕仍待實測
- 放大鏡方位在拖曳開始時決定，長距離反向拖曳時可能離游標較遠

---

## 2026-10-31 - v1.0.0

### 目標

建立 Windows 剪貼簿／畫面擷取工具「Clippings 剪貼簿」，可透過全域快捷鍵擷取畫面、簡易註釋、複製與儲存圖檔，並保留擷取與剪貼簿歷史。

### 變更

- `src/Clippings.Core/`：新增擷取／剪貼簿模型、`Annotation`、`PixelRect`、`UndoStack`、快捷鍵解析、設定、雜湊與路徑工具
- `src/Clippings.Platform/`：BitBlt 虛擬螢幕擷取、螢幕列舉與 DPI、SkiaSharp 編碼／縮圖／裁切、EF Core + SQLite 儲存、JSON 設定、開機啟動、單一實例、Win32 interop
- `src/Clippings.App/`：WPF 主視窗（歷史庫／剪貼簿歷史／設定）、擷取覆蓋層（凍結畫面、放大鏡、浮動工具列）、編輯器（10 種工具、undo/redo、縮放平移、複製／儲存）、系統匣、全域熱鍵、剪貼簿監聽、深淺色主題、檔案日誌
- `tests/Clippings.Core.Tests/`：UndoStack、PixelRect、HotkeyGesture、設定與雜湊共 21 項測試
- 文件：README、CHANGELOG、RELEASE_NOTES、AGENTS.md 專案補充

### 問題與解法

- **UI 框架改為 WPF**：原規劃 WinUI 3，但使用者選擇 WPF。查證後確認 `Microsoft.Graphics.Win2D` 沒有 WPF 控制項裝載，改用 SkiaSharp（`SkiaSharp.Views.WPF` 提供 `SKElement`）。此偏離已註記於 AGENTS.md。
- **SQLite 不支援 `DateTimeOffset` 排序／比較**：EF Core 查詢擲出 `NotSupportedException`。實體改存 UTC `DateTime`，模型仍使用 `DateTimeOffset` 並在對應層轉換。
- **XAML 初始化期間事件過早觸發**：編輯器 `RadioButton` 的 `Checked` 在 `InitializeComponent` 期間執行，存取尚未建立的 `CanvasElement` 造成 NRE。處理常式加入初始化完成前的防護。
- **單一實例通知卡住**：`NamedPipeClientStream.ConnectAsync(CancellationToken)` 在等待時未可靠回應取消，第二個實例不會結束。改為同步 `Connect(1500)` + `WriteByte`，並以 `Environment.Exit(0)` 結束第二實例。
- **建置輸出路徑不一致**：`PlatformTarget=x64` 未固定 `Platform`，導致 `bin/Debug` 與 `bin/x64/Debug` 交替，測試時執行到舊組建。`Directory.Build.props` 明確定義 `Platforms`/`Platform` 為 x64。
- **編輯器文字 API**：SkiaSharp 4 的 `SKPaint.TextSize`、`SKPath.MoveTo/LineTo` 已過時；改用 `SKFont` + `DrawText` 與 `DrawPoints`。

### 驗證

- `dotnet build`：0 警告、0 錯誤（`TreatWarningsAsErrors`）
- `dotnet test`：21 項通過
- 自動化實機驗證：全域熱鍵觸發、框選 450×300、剪貼簿圖片尺寸正確、歷史庫寫入 SQLite 與圖檔、編輯器開啟與複製（輸出含註釋像素）、單一實例僅保留一個程序

### 待辦／已知限制

- 視窗擷取（WGC）、滾動長截圖、螢幕錄影
- 跨螢幕框選、混合 DPI 多螢幕實測（目前僅 100% 單螢幕驗證）
- 多語系資源檔（目前文字集中於 `AppStrings`）、MSIX 安裝檔
- 編輯器裁切後清空 undo；檔案拖放剪貼簿項目僅以文字還原

### 備註

- 依 AGENTS.md，Release 建置與發佈僅在使用者明確要求時執行，本次僅使用 Debug。
