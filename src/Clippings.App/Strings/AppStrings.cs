using Clippings.Core.Models;

namespace Clippings.App.Strings;

public static class AppStrings
{
    public const string AppName = "Clippings 剪貼簿";
    public const string EditorTitle = "Clippings 編輯器";
    public const string LibraryTitle = "最近擷取";
    public const string ClipboardHistoryTitle = "剪貼簿歷史";
    public const string SettingsTitle = "設定";
    public const string ThemeSystem = "跟隨系統";
    public const string ThemeLight = "淺色";
    public const string ThemeDark = "深色";
    public const string ThemeWarmPaper = "暖色紙張";
    public const string ThemeSoftSage = "柔和鼠尾草";
    public const string ThemeDuskBlue = "午夜藍";
    public const string ThemeDuskPurple = "暮紫";
    public const string ThemeInkGreenNight = "墨綠夜";
    public const string ThemeMistBlueMorning = "霧藍晨光";

    public const string CaptureRegion = "區域擷取";
    public const string CaptureFullScreen = "全螢幕";
    public const string CaptureDelayed = "延遲擷取";
    public const string CaptureWindow = "視窗擷取";
    public const string ScreenAnnotation = "螢幕標註";
    public const string ScreenAnnotationToolTip = "開始螢幕標註";
    public const string StopScreenAnnotation = "結束標註";
    public const string StopScreenAnnotationToolTip = "結束目前的螢幕標註";
    public const string StartWindowRecording = "選擇視窗錄影";
    public const string StartFullScreenRecording = "全螢幕錄影";
    public const string RecordingFullScreen = "全螢幕";
    public const string StopRecording = "停止錄影";
    public const string PauseRecording = "暫停錄影";
    public const string ResumeRecording = "繼續錄影";
    public const string RecordingPausedStatus = "已暫停";
    public const string OpenLastRecording = "開啟上次錄影";
    public const string OpenRecordingFolder = "開啟錄影資料夾";
    public const string CaptureSourceFromClipboard = "剪貼簿匯入";
    public const string CaptureCursor = "擷取滑鼠游標";
    public const string CaptureCursorToolTip = "開啟後所有截圖模式都會包含滑鼠游標（預設關閉）";

    public const string ActionUndo = "復原";
    public const string ActionRedo = "重做";
    public const string ActionFitWindow = "符合視窗";
    public const string UndoToolTip = "復原 (Ctrl+Z)";
    public const string RedoToolTip = "重做 (Ctrl+Y)";
    public const string FitWindowToolTip = "縮放至符合視窗 (Ctrl+0)";
    public const string ActionCut = "剪下";
    public const string ActionCopy = "複製";
    public const string ActionPaste = "貼上";
    public const string ActionSelectAll = "全選";
    public const string ActionEdit = "編輯";
    public const string ActionSave = "儲存";
    public const string ActionSaveAs = "另存新檔";
    public const string SaveAsToolTip = "另存新檔為圖檔 (Ctrl+Shift+S)";
    public const string SaveToLibraryToolTip = "儲存編輯結果到最近擷取並關閉編輯器 (Ctrl+S)";
    public const string ActionDiscard = "捨棄";
    public const string DiscardToolTip = "刪除這張新擷圖並關閉編輯器（不儲存編輯結果）";
    public const string ActionClose = "關閉";
    public const string CloseEditorToolTip = "關閉編輯器，不儲存未儲存的編輯結果（與視窗右上角 X 相同）";
    public const string ActionCancel = "取消";
    public const string ActionDelete = "刪除";
    public const string ActionDeleteAll = "刪除全部";
    public const string ActionRefresh = "重新整理";
    public const string ActionAddToCapture = "加入擷取";
    public const string ActionAddToCaptureToolTip = "載入到最近擷取";

    public const string ToolPan = "平移";
    public const string ToolPanToolTip = "平移（或按滑鼠右鍵）";
    public const string ToolPen = "畫筆";
    public const string ToolHighlighter = "螢光筆";
    public const string ToolText = "文字";
    public const string ToolNumber = "編號";
    public const string ToolLine = "直線";
    public const string ToolArrow = "箭頭";
    public const string ToolRectangle = "矩形";
    public const string ToolEllipse = "橢圓";
    public const string ToolMosaic = "馬賽克";
    public const string ToolEraser = "橡皮擦";
    public const string ToolEraserToolTip = "橡皮擦：拖曳擦除碰到的註解";
    public const string ToolCrop = "裁切";
    public const string ToolCropToolTip = "裁切圖片，確認後才套用";

    public const string RotateRight = "右轉";
    public const string RotateRightToolTip = "右轉 90 度（註解將合併）";
    public const string RotateLeft = "左轉";
    public const string RotateLeftToolTip = "左轉 90 度（註解將合併）";
    public const string FlipVertical = "垂直翻轉";
    public const string FlipVerticalToolTip = "垂直翻轉（上下鏡像，註解將合併）";
    public const string FlipHorizontal = "水平翻轉";
    public const string FlipHorizontalToolTip = "水平翻轉（左右鏡像，註解將合併）";

    public const string EmptyCaptureHistory = "還沒有任何擷取，按 Ctrl+Shift+X 開始吧。";
    public const string EmptyClipboardImages = "目前沒有圖片紀錄。";
    public const string EmptySearchResult = "找不到符合的項目。";

    public const string SettingsSaved = "設定已儲存。";
    public const string InvalidHotkey = "快捷鍵格式不正確，請使用例如 Ctrl+Shift+X。";
    public const string HotkeyInUse = "快捷鍵已被其他程式佔用，請改用其他組合。";
    public const string CaptureFailed = "擷取失敗，請稍後再試。";
    public const string ClipboardCopied = "已複製到剪貼簿。";
    public const string ClipboardCopiedSelection = "已複製選取範圍到剪貼簿。";
    public const string ClipboardCopyFailed = "複製失敗，剪貼簿可能被其他程式佔用。";
    public const string CopyImageMissingFile = "圖片檔已不存在，無法複製。";
    public const string FileMissing = "檔案已不存在。";
    public const string FilePathCopied = "已複製檔案路徑。";
    public const string FilePathCopyFailed = "複製檔案路徑失敗，剪貼簿可能被其他程式佔用。";
    public const string RevealedInExplorer = "已在檔案總管中顯示。";
    public const string RevealInExplorerFailed = "無法在檔案總管中顯示檔案。";
    public const string Saved = "已儲存。";
    public const string DeleteConfirmTitle = "刪除項目";
    public const string DeleteConfirmMessage = "確定要刪除這個項目嗎？此動作無法復原。";
    public const string DeleteAllCapturesToolTip = "刪除所有未收藏的擷取；收藏的圖片會保留，需自行手動刪除";
    public const string DeleteAllCapturesConfirmTitle = "刪除全部擷取";
    public const string DeleteAllCapturesConfirmMessage = "確定要刪除所有未收藏的擷取嗎？收藏的圖片不會被刪除，需要自行手動刪除。此動作無法復原。";
    public const string DeleteAllCapturesNone = "沒有可刪除的未收藏擷取。";
    public const string ClearClipboardConfirmMessage = "確定要清除所有未釘選的剪貼簿歷史嗎？";
    public const string SavedToLibrary = "已加入 Clippings 歷史庫。";
    public const string AddToCaptureMissingFile = "圖片檔已不存在，無法加入最近擷取。";
    public const string CaptureHistorySaveFailed = "擷取已完成，但無法寫入歷史庫。";
    public const string ClipboardHistorySaveFailed = "無法寫入剪貼簿歷史。";
    public const string AutoSaveFailed = "擷取已完成，但自動存檔失敗。";
    public const string CommandLineForwardFailed = "無法連線到執行中的 Clippings，指令未送出。";
    public const string RecordingStarted = "已開始錄影。";
    public const string RecordingUnsupported = "此 Windows 版本或顯示環境不支援螢幕錄影。";
    public const string RecordingTargetUnavailable = "所選視窗已無法錄製，請重新選擇。";
    public const string RecordingMonitorUnavailable = "找不到可錄製的螢幕。";
    public const string RecordingResizeWarning = "錄影開始後若調整所選視窗大小或將視窗最小化，錄影會自動停止。";
    public const string RecordingTrayToolTip = "Clippings 剪貼簿（正在錄影）";
    public const string RecordingTrayToolTipPaused = "Clippings 剪貼簿（錄影已暫停）";
    public const string CommandLineUsage = """
        Clippings 命令列參數

          Clippings.exe [參數]

          --show              顯示主視窗
          --capture-region    區域擷取
          --capture-full      全螢幕擷取
          --capture-monitor   目前螢幕擷取
          --capture-window    目前視窗擷取
          --annotate          開關螢幕標註模式
          --autostart         開機啟動時不顯示主視窗
          --help              顯示此說明

        未指定擷取參數時，重複啟動只會顯示主視窗。
        擷取指令會轉送給已執行的 Clippings；若尚未執行，會先啟動並常駐系統匣。
        指令送出後即結束，不代表擷取已完成。
        """;

    public static string FormatInvalidCommandLineArgument(string argument)
        => $"無法辨識或重複的參數：{argument}";

    public static string FormatAddToCaptureFailed(string message)
        => $"加入擷取失敗：{message}";

    public static string FormatSaveToLibraryFailed(string message)
        => $"存入最近擷取失敗：{message}";

    public static string FormatDiscardFailed(string message)
        => $"捨棄失敗：{message}";

    public static string FormatDeleteAllCapturesResult(int count)
        => $"已刪除 {count} 張未收藏的擷取；收藏的圖片已保留。";

    public static string FormatRecordingSaved(string path, RecordingStopReason reason)
    {
        var detail = reason switch
        {
            RecordingStopReason.TargetClosed => "目標視窗已關閉，錄影已自動停止。",
            RecordingStopReason.TargetResized => "目標視窗尺寸已改變，錄影已自動停止。",
            RecordingStopReason.TargetMinimized => "目標視窗已最小化，錄影已自動停止。",
            _ => "錄影已儲存。",
        };
        return $"{detail}{Environment.NewLine}{path}";
    }

    public static string FormatRecordingFailed(string message)
        => $"錄影失敗：{message}";

    public static string FormatOpenRecordingFailed(string message)
        => $"無法開啟錄影：{message}";

    public static string FormatSize(int width, int height) => $"{width} × {height}";
}

