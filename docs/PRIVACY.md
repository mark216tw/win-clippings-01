# 隱私說明

生效日期：2026-10-31

Clippings 是離線的 Windows 桌面應用程式。本文件說明程式會處理哪些資料、存放在哪裡，以及使用者如何控制與刪除。

---

## 1. 摘要

- **不連線**：程式沒有網路功能，不會上傳截圖、剪貼簿內容、設定或日誌。
- **不收集遙測**：沒有帳號、沒有使用統計、沒有崩潰回報服務。
- **本機儲存**：所有資料位於目前使用者的 `%LOCALAPPDATA%\Clippings`。
- **可完全關閉監聽**：剪貼簿歷史可在設定頁停用。
- **可完全刪除**：刪除資料夾與相關登錄值即可清除。

---

## 2. 程式處理的資料

| 資料 | 來源 | 用途 | 位置 |
| --- | --- | --- | --- |
| 螢幕擷取影像 | 使用者主動擷取 | 複製、編輯、存檔、歷史庫 | `images\`、`clipboard\` |
| 縮圖 | 由擷取影像產生 | 歷史列表預覽 | `thumbs\` |
| 擷取中繼資料 | 擷取流程 | 歷史查詢、收藏、保留策略 | `clippings.db` |
| 剪貼簿內容 | 系統剪貼簿（可關閉） | 剪貼簿歷史 | `clippings.db`、`clipboard\` |
| 設定 | 使用者在設定頁輸入 | 記住偏好 | `settings.json` |
| 日誌 | 程式執行錯誤與事件 | 排錯 | `logs\` |
| 開機啟動設定 | 使用者勾選 | 登入時啟動 | `HKCU\...\Run` |

程式**不會**讀取瀏覽器密碼、檔案內容或網路流量。

---

## 3. 剪貼簿監聽

- 預設開啟，可在設定頁關閉。
- 監聽 Windows 的 `WM_CLIPBOARDUPDATE` 訊息。
- 僅記錄複製的圖片；文字與檔案清單不記錄，也不記錄其他自訂格式。
- 相同內容以 SHA-256 去重。
- 關閉後停止監聽與寫入，既有紀錄仍保留，直到使用者清除。

**提醒**：若複製的圖片包含帳號畫面、證件或其他敏感內容，可能會被寫入本機資料庫。請視需要關閉剪貼簿歷史或定期清除。

---

## 4. 日誌

- 日誌存放於 `%LOCALAPPDATA%\Clippings\logs\app-YYYYMMDD.log`。
- 只保留最近 7 天，啟動時自動清除較舊檔案。
- 日誌可能包含錯誤訊息、檔案路徑與元件名稱，但不會主動記錄完整剪貼簿內容。
- 回報問題時，請先檢查日誌是否含有敏感資訊再分享。

---

## 5. 網路與第三方

- 程式本體沒有網路呼叫。
- 安裝與更新由使用者手動下載，不包含自動更新器。
- 第三方套件（SkiaSharp、EF Core、CommunityToolkit.Mvvm、H.NotifyIcon 等）在本機執行，不會主動傳送資料。
- GitHub 網站與 Release 下載受 GitHub 自身的隱私政策規範。

---

## 6. 資料保留與刪除

- 擷取與剪貼簿項目可設定數量與天數上限。
- 收藏與釘選項目不受自動清除影響。
- 使用者可在介面中刪除單一項目或清除未釘選的剪貼簿歷史。
- 完整清除方式：

```powershell
# 先結束 Clippings
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Clippings"

# 移除開機啟動（若曾啟用）
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "Clippings" -ErrorAction SilentlyContinue

# 若曾自動存檔，另外檢查
# %USERPROFILE%\Pictures\Clippings
```

---

## 7. 兒童與敏感資料

本程式未針對兒童設計，也不會主動判斷內容是否敏感。使用者應自行避免在共用電腦上保留含敏感資訊的截圖或剪貼簿紀錄。

---

## 8. 變更與聯絡

若本說明有重大變更，會更新本文件並記錄於 `CHANGELOG.md`。

隱私相關問題請透過 GitHub Issues 聯絡：

<https://github.com/mark216tw/win-clippings-01/issues>
