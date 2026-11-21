# 隱私說明

生效日期：2026-11-11

Clippings 是離線的 Windows 桌面應用程式。本文件說明程式會處理哪些資料、存放在哪裡，以及使用者如何控制與刪除。

---

## 1. 摘要

- **不連線**：程式沒有網路功能，不會上傳截圖、設定或日誌。
- **不收集遙測**：沒有帳號、沒有使用統計、沒有崩潰回報服務。
- **本機儲存**：應用程式資料位於 `%LOCALAPPDATA%\Clippings.App.New`；匯出的錄影預設位於使用者的 `影片\Clippings.App.New`（可自訂），不會上傳。
- **可完全刪除**：刪除資料夾與相關登錄值即可清除。

---

## 2. 程式處理的資料

| 資料 | 來源 | 用途 | 位置 |
| --- | --- | --- | --- |
| 螢幕擷取影像 | 使用者主動擷取 | 複製、編輯、存檔、頁籤暫存 | `workspace\` |
| 螢幕錄影與可選的系統音訊 | 使用者從主視窗或系統匣主動開始 | 本機 MP4 錄影 | `%USERPROFILE%\Videos\Clippings.App.New` 或使用者指定資料夾 |
| 設定 | 使用者在設定頁輸入 | 記住偏好 | `settings.json` |
| 日誌 | 程式執行錯誤與事件 | 排錯 | `logs\` |
| 開機啟動設定 | 使用者勾選 | 登入時啟動 | `HKCU\...\Run` |

程式**不會**讀取瀏覽器密碼、檔案內容或網路流量。

錄製電腦聲音預設關閉。啟用後會擷取預設播放裝置的混音，可能包含媒體、通知與通訊聲音，但不會擷取麥克風；DRM 或獨占模式音訊可能無法錄製。

---

## 3. 日誌

- 日誌存放於 `%LOCALAPPDATA%\Clippings.App.New\logs\app-YYYYMMDD.log`。
- 只保留最近 7 天，啟動時自動清除較舊檔案。
- 日誌可能包含錯誤訊息、檔案路徑與元件名稱，但不會主動記錄完整圖片內容。
- 回報問題時，請先檢查日誌是否含有敏感資訊再分享。

---

## 4. 網路與第三方

- 程式本體沒有網路呼叫。
- 安裝與更新由使用者手動下載，不包含自動更新器。
- 第三方套件（SkiaSharp、CommunityToolkit.Mvvm、H.NotifyIcon、NAudio 等）在本機執行，不會主動傳送資料。
- GitHub 網站與 Release 下載受 GitHub 自身的隱私政策規範。

---

## 5. 資料保留與刪除

- 擷圖頁籤暫存檔會在關閉頁籤或另存新檔後刪除；關閉後有 6 秒吐司可復原，逾時或關閉吐司才刪除。關閉全部頁籤同樣在吐司結束後刪除所有暫存檔。
- 完整清除方式：

```powershell
# 先結束 Clippings 剪貼簿
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Clippings.App.New"

# 移除開機啟動（若曾啟用）
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "Clippings.App.New" -ErrorAction SilentlyContinue

# 若曾自動存檔，另外檢查
# %USERPROFILE%\Pictures\Clippings.App.New

# 若曾錄影，另外檢查
# %USERPROFILE%\Videos\Clippings.App.New
```

---

## 6. 兒童與敏感資料

本程式未針對兒童設計，也不會主動判斷內容是否敏感。使用者應自行避免在共用電腦上保留含敏感資訊的截圖。

---

## 7. 變更與聯絡

若本說明有重大變更，會更新本文件並記錄於 `CHANGELOG.md`。

隱私相關問題請透過 GitHub Issues 聯絡：

<https://github.com/mark216tw/win-clippings-02/issues>
