using System;
using System.Collections.Generic;
using Godot;

namespace STS2Mobile.Launcher;

// Launcher translations, registered with Godot's TranslationServer so lookups go
// through the same mechanism the engine and the game use.
//
// Translations are built in code rather than loaded from .po files because the
// launcher runs on a bootstrap PCK with no importable resources: a .po would
// need the editor's import step, which never runs for these strings.
public static class Localization
{
    private const string FallbackLocale = "en";

    private static readonly Dictionary<string, string> Korean = new()
    {
        ["LAUNCHER_TITLE"] = "슬레이 더 스파이어 II 런처",
        ["MENU_PLAY"] = "시작",
        ["MENU_RETRY"] = "다시 시도",
        ["MENU_NEWS"] = "소식",
        ["MENU_SETTINGS"] = "설정",
        ["MENU_CONSOLE"] = "콘솔",
        ["MENU_UPDATE_LAUNCHER"] = "런처 업데이트",
        ["ACTION_CLOSE"] = "닫기",
        ["ACTION_COPY_LOG"] = "클립보드로 복사",
        ["SETTING_LOCAL_BACKUP"] = "로컬 백업",
        ["SETTING_AUTO_SYNC"] = "자동 동기화",
        ["SETTING_BETA_CHANNEL"] = "베타 채널",
        ["SETTING_FPS_OVERLAY"] = "성능 표시",
        ["SETTING_CLOUD_HEADER"] = "클라우드 세이브",
        ["SETTING_UPLOAD_SAVES"] = "세이브 올리기",
        ["SETTING_DOWNLOAD_SAVES"] = "세이브 내려받기",
        ["SETTING_CHECK_UPDATES"] = "업데이트 확인",
        ["NEWS_HEADER"] = "스팀 소식",
        ["NEWS_LOADING"] = "불러오는 중…",
        ["STATUS_INITIALIZING"] = "준비 중…",
        ["STATUS_COMPILING_SHADERS"] = "셰이더 컴파일 중",
        ["STATUS_ENUMERATING"] = "리소스 확인 중",
        ["WELCOME_BACK"] = "{0}님, 환영합니다",
        ["DIALOG_CONFIRM_TITLE"] = "확인",
        ["DIALOG_YES"] = "예",
        ["DIALOG_NO"] = "아니요",
        ["MENU_QUIT"] = "종료",
        ["CLOUD_PUSH_CONFIRM"] = "로컬 세이브를 클라우드에 올릴까요?\n클라우드 세이브를 덮어씁니다.",
        ["CLOUD_PULL_CONFIRM"] = "클라우드 세이브를 내려받을까요?\n로컬 세이브를 덮어씁니다.",
        ["CLOUD_PUSH_RUNNING"] = "세이브를 올리는 중…",
        ["CLOUD_PULL_RUNNING"] = "세이브를 내려받는 중…",
        ["CLOUD_DONE"] = "완료됐습니다.",
        ["CLOUD_FAILED"] = "실패: {0}",
        ["UPDATE_UP_TO_DATE"] = "최신 버전입니다",
        ["DOWNLOAD_GAME_FILES"] = "게임 파일 받기",
        ["DOWNLOAD_IN_PROGRESS"] = "게임 파일 다운로드 중",
        ["QUIT_CONFIRM"] = "정말 종료하시겠습니까?",
        ["STATE_ON"] = "켬",
        ["STATE_OFF"] = "끔",
        ["NEWS_EMPTY"] = "(최근 공지 없음)",
        ["NEWS_UNAVAILABLE"] = "(소식을 불러올 수 없음)",
        ["STATUS_SCANNING_SHADERS"] = "셰이더 검색 중",
        ["STATUS_DONE"] = "완료",
        ["UPDATE_TAP_TO_INSTALL"] = "눌러서 설치",
        ["UPDATE_ALLOW_INSTALL"] = "설정에서 설치 허용 필요",
    };

    // English doubles as the key documentation: every key used anywhere must
    // appear here, and Tr falls back to this table before returning the key.
    private static readonly Dictionary<string, string> English = new()
    {
        ["LAUNCHER_TITLE"] = "Slay the Spire II Launcher",
        ["MENU_PLAY"] = "PLAY",
        ["MENU_RETRY"] = "RETRY",
        ["MENU_NEWS"] = "News",
        ["MENU_SETTINGS"] = "Settings",
        ["MENU_CONSOLE"] = "Console",
        ["MENU_UPDATE_LAUNCHER"] = "UPDATE LAUNCHER",
        ["ACTION_CLOSE"] = "Close",
        ["ACTION_COPY_LOG"] = "Copy to clipboard",
        ["SETTING_LOCAL_BACKUP"] = "Local Backup",
        ["SETTING_AUTO_SYNC"] = "Auto Sync",
        ["SETTING_BETA_CHANNEL"] = "Beta Channel",
        ["SETTING_FPS_OVERLAY"] = "FPS Overlay",
        ["SETTING_CLOUD_HEADER"] = "Cloud Saves",
        ["SETTING_UPLOAD_SAVES"] = "Upload saves",
        ["SETTING_DOWNLOAD_SAVES"] = "Download saves",
        ["SETTING_CHECK_UPDATES"] = "Check for Updates",
        ["NEWS_HEADER"] = "Steam News",
        ["NEWS_LOADING"] = "Loading…",
        ["STATUS_INITIALIZING"] = "Initializing…",
        ["STATUS_COMPILING_SHADERS"] = "Compiling shaders",
        ["STATUS_ENUMERATING"] = "Enumerating resources",
        ["WELCOME_BACK"] = "Welcome back, {0}",
        ["DIALOG_CONFIRM_TITLE"] = "Confirm",
        ["DIALOG_YES"] = "Yes",
        ["DIALOG_NO"] = "No",
        ["MENU_QUIT"] = "Quit",
        ["CLOUD_PUSH_CONFIRM"] = "Upload local saves to the cloud?\nThis overwrites your cloud saves.",
        ["CLOUD_PULL_CONFIRM"] = "Download cloud saves?\nThis overwrites your local saves.",
        ["CLOUD_PUSH_RUNNING"] = "Uploading saves…",
        ["CLOUD_PULL_RUNNING"] = "Downloading saves…",
        ["CLOUD_DONE"] = "Done.",
        ["CLOUD_FAILED"] = "Failed: {0}",
        ["UPDATE_UP_TO_DATE"] = "Up to date",
        ["DOWNLOAD_GAME_FILES"] = "Download game files",
        ["DOWNLOAD_IN_PROGRESS"] = "Downloading game files",
        ["QUIT_CONFIRM"] = "Are you sure you want to quit?",
        ["STATE_ON"] = "ON",
        ["STATE_OFF"] = "OFF",
        ["NEWS_EMPTY"] = "(no recent announcements)",
        ["NEWS_UNAVAILABLE"] = "(news unavailable)",
        ["STATUS_SCANNING_SHADERS"] = "Scanning for shaders",
        ["STATUS_DONE"] = "Done",
        ["UPDATE_TAP_TO_INSTALL"] = "TAP TO INSTALL",
        ["UPDATE_ALLOW_INSTALL"] = "ALLOW INSTALL IN SETTINGS",
    };

    private static bool _installed;

    public static bool IsKorean { get; private set; }

    public static void Install()
    {
        if (_installed)
            return;
        _installed = true;

        try
        {
            AddTranslation("en", English);
            AddTranslation("ko", Korean);

            // OS.GetLocale returns forms like "ko_KR"; the language part is what
            // decides which table applies.
            var locale = OS.GetLocale() ?? FallbackLocale;
            var language = locale.Split('_', '-')[0].ToLowerInvariant();
            IsKorean = language == "ko";

            TranslationServer.SetLocale(IsKorean ? "ko" : FallbackLocale);
            PatchHelper.Log($"[i18n] locale={locale} using={(IsKorean ? "ko" : FallbackLocale)}");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[i18n] setup failed, staying on English: {ex.Message}");
        }
    }

    private static void AddTranslation(string locale, Dictionary<string, string> messages)
    {
        var translation = new Translation { Locale = locale };
        foreach (var (key, value) in messages)
            translation.AddMessage(key, value);
        TranslationServer.AddTranslation(translation);
    }

    public static string Tr(string key)
    {
        Install();

        var translated = TranslationServer.Translate(key);
        if (!string.IsNullOrEmpty(translated) && translated != key)
            return translated;

        // A missing translation should still read as English rather than as a
        // raw key leaking into the UI.
        return English.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public static string Tr(string key, params object[] args) => string.Format(Tr(key), args);
}
