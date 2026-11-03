# 貢獻指南

感謝你對 Clippings 有興趣。本專案以 MIT License 發佈，歡迎送出 Issue、改進程式或文件。

---

## 1. 開發環境

- Windows 10 1809（build 17763）以上，x64。
- .NET SDK 10.0。
- 建議使用 Visual Studio、Rider 或 VS Code。

```powershell
dotnet restore Clippings.sln
dotnet build Clippings.sln
dotnet test Clippings.sln
dotnet format Clippings.sln --verify-no-changes
```

開發與測試請使用 `Debug`；除非要驗證發佈流程，不要建立 `Release`。

---

## 2. 專案慣例

- 使用 C# nullable、file-scoped namespace、4 空格縮排。
- 公開成員 PascalCase、區域變數 camelCase、私有欄位 `_camelCase`。
- 非同步方法以 `Async` 結尾；UI 層不加 `ConfigureAwait(false)`，函式庫層加。
- MVVM 使用 `CommunityToolkit.Mvvm`，不要手寫 `INotifyPropertyChanged`。
- 新套件必須先加入 `Directory.Packages.props`，不要在 `.csproj` 寫死版本。
- 使用者可見文字集中於 `src/Clippings.App/Strings/AppStrings.cs`。
- 不要關閉警告；`TreatWarningsAsErrors` 已啟用。
- 不要提交 `bin/`、`obj/`、`.vs/`、發佈輸出、日誌或憑證。

詳細規範見 `AGENTS.md`。

---

## 3. 變更流程

1. 先開 Issue 描述問題或需求（小型修正可直接開 PR）。
2. 從 `main` 建立功能分支。
3. 完成程式與測試。
4. 更新 `docs/DEV_LOG.md`，記錄目標、變更、問題與驗證。
5. 若有功能變更，更新 `CHANGELOG.md` 與 `RELEASE_NOTES.md`，並依規範調整版本。
6. 執行建置、測試與格式檢查。
7. 送出 Pull Request，說明動機、做法與驗證方式。

### Commit 訊息

建議使用簡短、命令式的描述，例如：

```
新增區域擷取快捷鍵自訂
修正高 DPI 螢幕框選偏移
更新使用指南與隱私說明
```

---

## 4. 測試

- 核心邏輯請補 xUnit 測試，放在 `tests/Clippings.Core.Tests`。
- UI、GDI 擷取與剪貼簿行為請在 PR 說明中列出人工驗證步驟。
- 不要留下 `TODO`、測試假資料或除錯輸出。

---

## 5. 文件

- 功能或行為變更需同步更新 `README.md` 與相關 `docs/*.md`。
- 使用者操作變更請更新 `docs/USER_GUIDE.md`。
- 架構或設計決策變更請更新 `docs/ARCHITECTURE.md`、`docs/TECHNICAL.md` 或 `docs/SYSTEM_DESIGN.md`。
- 資安與隱私相關變更請更新 `SECURITY.md` 或 `docs/PRIVACY.md`。

---

## 6. 授權

送出貢獻即表示你同意以本專案的 MIT License 授權你的貢獻。
