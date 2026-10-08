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
        ["VERSION_TITLE"] = "버전 정보",
        ["VERSION_LAUNCHER"] = "런처",
        ["VERSION_GAME_FILES"] = "다운로드한 게임",
        ["VERSION_GAME_DLL"] = "실행 중인 게임 DLL",
        ["VERSION_ENGINE"] = "Godot 엔진",
        ["VERSION_RUNTIME"] = ".NET 런타임",
        ["VERSION_UNAVAILABLE"] = "정보 없음",
        ["VERSION_COPY"] = "버전 정보 복사",
        ["BENCH_RESULTS_TAB"] = "측정 결과",
        ["BENCH_CAPTURE_TAB"] = "화면 비교",
        ["BENCH_SCENE_CARDS"] = "카드",
        ["BENCH_SCENE_GEOMETRY"] = "도형",
        ["BENCH_SCENE_EFFECTS"] = "화면 효과",
        ["BENCH_FRAME_TIMES"] = "프레임 평균 / p95 / p99",
        ["BENCH_RESULTS_HELP"] =
            "같은 장면의 결과끼리 비교하세요. CPU·GPU는 테스트 장면의 렌더링 시간이며, 실제 전투 전체나 배터리 소비량은 아닙니다.",
        ["BENCH_NO_CAPTURE"] = "아직 저장된 비교 화면이 없습니다.",
        ["BENCH_READY"] = "자동 비교를 시작하면 옵션 적용과 재시작까지 진행합니다.",
        ["BENCH_COMPLETED"] = "자동 비교가 완료됐습니다. 원래 설정으로 복귀했습니다.",
        ["BENCH_CANCELLED"] = "중단됐습니다. 완료한 항목의 결과는 보관했습니다.",
        ["BENCH_INTERRUPTED"] =
            "이전 테스트가 끊겼습니다. 완료한 결과를 확인하거나 다시 시작할 수 있습니다.",
        ["BENCH_TITLE"] = "렌더링 벤치마크",
        ["BENCH_INFO"] =
            "고정된 카드·도형·화면 효과 장면에서 옵션을 자동 비교합니다. 페이싱 적용을 위한 재시작도 자동으로 진행합니다. 테스트 중에는 조작하거나 화면을 접지 말아 주세요. 완료 후 원래 설정으로 돌아오며 결과와 비교 화면은 오프라인으로 보관됩니다.",
        ["BENCH_START"] = "자동 비교 시작",
        ["BENCH_CANCEL"] = "중단",
        ["BENCH_CLOSE"] = "돌아가기",
        ["BENCH_COPY"] = "전체 결과 복사",
        ["BENCH_COPIED"] = "전체 결과를 복사했습니다.",
        ["BENCH_NO_RESULT"] = "저장된 결과가 없습니다.",
        ["BENCH_ERROR"] = "벤치마크를 시작할 수 없습니다.",
        ["BENCH_RESTART"] = "다음 테스트를 위해 자동 재시작합니다…",
        ["BENCH_WARM"] = "예열 중",
        ["BENCH_MEASURE"] = "측정 중",
        ["GRAPHICS_PRESET"] = "그래픽 프리셋",
        ["GRAPHICS_PRESET_QUALITY"] = "화질 우선",
        ["GRAPHICS_PRESET_BALANCED"] = "균형",
        ["GRAPHICS_PRESET_BATTERY"] = "절전",
        ["GRAPHICS_PRESET_CUSTOM"] = "사용자 설정",
        ["GRAPHICS_PRESET_INFO"] =
            "게임 기본값은 화질 옵션을 원래 설정으로 되돌립니다. 화질 우선은 윤곽선과 텍스처 품질을 높이고, 균형은 해상도와 효과를 일부 줄입니다. 절전은 해상도를 낮추고 흐림·왜곡·배경 입자를 끕니다.\n\n프레임 제한, 성능 표시, 셰이더 예열, 프레임 페이싱은 유지됩니다. 절전과 함께 FPS를 낮추면 그리는 횟수도 줄일 수 있습니다. 실험적인 카드 직접 그리기는 프리셋에서 사용하지 않습니다. 다음 시작부터 적용됩니다.",
        ["GRAPHICS_RESOLUTION_INFO"] =
            "화면을 그리는 내부 해상도입니다. 낮출수록 처리할 픽셀이 줄어 GPU 부하를 낮출 수 있지만, 카드와 글자도 흐려질 수 있습니다.\n\n화면 배치와 터치 좌표는 유지됩니다. 다음 시작부터 적용됩니다.",
        ["GRAPHICS_MSAA_INFO"] =
            "도형의 가장자리를 여러 번 샘플링하여 계단 모양을 줄입니다. 높을수록 GPU 작업과 메모리 사용이 늘 수 있으며, 모든 이미지나 글자가 개선되는 것은 아닙니다.\n\n게임 기본값은 게임 설정을 따릅니다. 다음 시작부터 적용되고, 변경하면 셰이더를 다시 예열합니다.",
        ["GRAPHICS_HDR_INFO"] =
            "2D 화면을 더 넓은 색 범위로 계산합니다. 일부 빛과 색 효과 표현에 도움이 되지만 렌더 버퍼의 메모리와 대역폭 사용이 늘 수 있습니다. 휴대폰 화면의 HDR 출력 기능을 켜는 옵션은 아닙니다.\n\n다음 시작부터 적용되고, 변경하면 셰이더를 다시 예열합니다.",
        ["GRAPHICS_FILTER_INFO"] =
            "이미지를 확대·축소할 때 픽셀을 섞는 방식입니다. 최근접은 픽셀 경계를 유지하고, 선형은 부드럽게 섞습니다. 밉맵은 작은 이미지의 반짝임을 줄이고, 이방성은 기울어진 텍스처의 선명도를 높일 수 있습니다.\n\n자체 필터를 지정한 이미지에는 영향을 주지 않습니다. 다음 시작부터 적용됩니다.",
        ["GRAPHICS_CARD_COMPOSITION_INFO"] =
            "원본은 게임의 카드 초상화 합성 방식을 유지합니다. 직접 그리기는 일반 카드의 중간 합성 단계를 줄이는 실험 옵션입니다. 별도 마스크가 필요한 카드는 원본 합성을 유지합니다.\n\n그림이나 효과가 이상하면 원본을 사용하세요. 다음 시작부터 적용됩니다.",
        ["GRAPHICS_RADIAL_BLUR_INFO"] =
            "특정 효과에서 화면을 방사형으로 흐리게 만드는 처리입니다. 낮음은 샘플 수를 줄이고, 끄기는 해당 흐림 처리와 화면 복사를 생략합니다. 효과가 나타나는 순간에만 차이가 납니다.\n\n다음 시작부터 적용됩니다.",
        ["GRAPHICS_DISTORTION_INFO"] =
            "화면 전체를 읽어 흔들거나 굴절시키는 일부 효과를 표시합니다. 끄면 해당 효과를 숨겨 관련 렌더 작업을 줄일 수 있습니다. 모든 애니메이션이나 화면 흔들기를 끄지는 않습니다.\n\n다음 시작부터 적용됩니다.",
        ["GRAPHICS_PARTICLES_INFO"] =
            "전투 배경의 움직이는 입자 수를 조절합니다. 줄이면 배경 효과가 덜 풍성해지지만 CPU·GPU 작업을 줄일 수 있습니다. 끄기는 해당 입자의 방출과 표시를 중단합니다.\n\n카드·캐릭터 효과는 유지됩니다. 다음 시작부터 적용됩니다.",
        ["GRAPHICS_WARMUP_INFO"] =
            "게임 시작 시 셰이더를 미리 준비하여 처음 효과가 나올 때의 끊김을 줄이려는 옵션입니다. 처음 준비하는 실행은 더 오래 걸리며, 완료된 예열은 다음 실행에서 생략합니다.\n\n끄면 사전 예열을 건너뜁니다. 아래의 다시 예열 버튼은 다음 시작에 준비를 다시 하도록 예약합니다.",
        ["GRAPHICS_PACING_INFO"] =
            "Android에서 프레임을 화면에 내보내는 간격을 조절합니다. 자동 FPS는 엔진이 간격을 조절하고, 자동 FPS + 파이프라인은 프레임 준비 방식도 조절합니다. 선택한 FPS 유지는 자동 간격 조절을 끕니다.\n\n위의 FPS 제한과는 별개이며, 이 옵션만으로 FPS나 배터리 개선을 보장하지 않습니다. 변경 후 시작을 누르면 앱을 재시작하여 적용합니다.",
        ["SETTING_FRAME_LIMIT_INFO"] =
            "초당 화면을 그리는 횟수의 상한입니다. 낮추면 CPU·GPU 작업을 줄일 수 있지만 움직임이 덜 부드러워집니다. 무제한은 이 상한을 해제하며 화면 주사율이나 프레임 페이싱에 따라 제한될 수 있습니다.\n\n기기의 성능과 발열 상태에 따라 선택한 FPS보다 낮게 나올 수 있습니다. 게임 시작 시 적용됩니다.",
        ["SETTING_FPS_OVERLAY_INFO"] =
            "게임 중 FPS와 선택한 성능 정보를 표시합니다. 끄면 표시창과 그 측정 작업을 함께 중단합니다.\n\n표시창을 끄더라도 게임의 프레임 제한과 그래픽 옵션은 유지됩니다.",
        ["SETTING_OVERLAY_CPU_INFO"] =
            "성능 표시창에 앱의 CPU 사용 정보를 표시합니다. 끄면 이 항목의 측정을 중단합니다. 성능 표시 자체가 꺼져 있으면 측정하지 않습니다.",
        ["SETTING_OVERLAY_GPU_INFO"] =
            "성능 표시창에 기기가 제공하는 GPU 사용 정보를 표시합니다. 끄면 이 항목의 측정을 중단합니다. 기기에 따라 값을 읽지 못할 수 있습니다.",
        ["SETTING_OVERLAY_TEMP_INFO"] =
            "성능 표시창에 기기가 제공하는 온도 정보를 표시합니다. 끄면 이 항목의 측정을 중단합니다. 센서 값은 기기에 따라 의미와 제공 여부가 다릅니다.",
        ["LAUNCHER_TITLE"] = "비공식 StS2 런처",
        ["UNOFFICIAL_NOTICE"] = "Mega Crit 또는 Valve와 제휴하거나 보증받은 앱이 아닙니다.",
        ["MENU_PLAY"] = "시작",
        ["MENU_RETRY"] = "다시 시도",
        ["MENU_NEWS"] = "소식",
        ["MENU_SETTINGS"] = "설정",
        ["MENU_GRAPHICS"] = "그래픽",
        ["MENU_GENERAL"] = "일반",
        ["GRAPHICS_HELP"] =
            "이 기기에만 저장됩니다. 다음 시작부터 적용되며 프레임 페이싱을 바꾸면 시작 시 앱을 재시작합니다. 해상도를 낮추면 글자도 흐려질 수 있습니다.",
        ["GRAPHICS_RESOLUTION"] = "렌더 해상도",
        ["GRAPHICS_MSAA"] = "윤곽선 품질 (MSAA)",
        ["GRAPHICS_HDR"] = "HDR 2D",
        ["GRAPHICS_FILTER"] = "텍스처 필터링",
        ["GRAPHICS_CARD_COMPOSITION"] = "카드 초상화 합성",
        ["GRAPHICS_RADIAL_BLUR"] = "화면 방사형 흐림",
        ["GRAPHICS_DISTORTION"] = "화면 왜곡",
        ["GRAPHICS_PARTICLES"] = "배경 입자",
        ["GRAPHICS_WARMUP"] = "셰이더 예열",
        ["GRAPHICS_PACING"] = "프레임 페이싱 · 재시작",
        ["GRAPHICS_GAME_DEFAULT"] = "게임 기본값",
        ["GRAPHICS_NEAREST"] = "최근접",
        ["GRAPHICS_LINEAR"] = "선형",
        ["GRAPHICS_MIPMAP"] = "선형 + 밉맵",
        ["GRAPHICS_ANISOTROPIC"] = "밉맵 + 이방성",
        ["GRAPHICS_ORIGINAL"] = "원본",
        ["GRAPHICS_DIRECT"] = "직접 그리기 (실험)",
        ["GRAPHICS_REDUCED"] = "낮음",
        ["GRAPHICS_PACING_AUTO"] = "자동 FPS + 파이프라인",
        ["GRAPHICS_PACING_AUTO_FPS"] = "자동 FPS",
        ["GRAPHICS_PACING_FIXED"] = "선택한 FPS 유지",
        ["GRAPHICS_REBUILD_WARMUP"] = "다음 시작에 셰이더 다시 예열",
        ["GRAPHICS_WARMUP_PENDING"] = "다음 시작에 셰이더를 다시 예열합니다.",
        ["GRAPHICS_WARMUP_DISABLED"] = "예열을 켜면 다음 시작에 다시 예열합니다.",
        ["GRAPHICS_SAVE_FAILED"] = "설정을 저장하지 못했습니다. 콘솔을 확인해주세요.",
        ["GRAPHICS_RESTART_PENDING"] = "저장됨 · 시작을 누르면 앱을 재시작해 적용합니다.",
        ["GRAPHICS_SAVED"] = "저장됨 · 다음 시작부터 적용됩니다.",
        ["MENU_CONSOLE"] = "콘솔",
        ["MENU_LEGAL"] = "법적 고지 및 개인정보",
        ["LEGAL_TITLE"] = "법적 고지 및 개인정보",
        ["LEGAL_LOAD_FAILED"] = "포함된 법적 고지를 불러올 수 없습니다.",
        ["MENU_UPDATE_LAUNCHER"] = "런처 업데이트",
        ["ACTION_CLOSE"] = "닫기",
        ["ACTION_COPY_LOG"] = "클립보드로 복사",
        ["SETTING_LOCAL_BACKUP"] = "로컬 백업",
        ["SETTING_AUTO_SYNC"] = "자동 동기화",
        ["SETTING_BETA_CHANNEL"] = "베타 채널",
        ["SETTING_FPS_OVERLAY"] = "성능 표시",
        ["SETTING_FRAME_LIMIT"] = "프레임 제한 (FPS)",
        ["FRAME_LIMIT_UNLIMITED"] = "무제한",
        ["SETTING_OVERLAY_CPU"] = "성능 표시 · CPU",
        ["SETTING_OVERLAY_GPU"] = "성능 표시 · GPU",
        ["SETTING_OVERLAY_TEMP"] = "성능 표시 · 온도",
        ["SETTING_CLOUD_HEADER"] = "클라우드 세이브",
        ["SETTING_UPLOAD_SAVES"] = "올리기",
        ["SETTING_DOWNLOAD_SAVES"] = "내려받기",
        ["SETTING_CHECK_UPDATES"] = "확인",
        ["SETTING_UPDATE_ROW"] = "게임 업데이트",
        ["NEWS_HEADER"] = "스팀 소식",
        ["NEWS_LOADING"] = "불러오는 중…",
        ["NEWS_BACK_TO_LIST"] = "← 목록",
        ["NEWS_OPEN_ORIGINAL"] = "원문 보기",
        ["NEWS_TRANSLATE"] = "번역 (필요 시 Google)",
        ["NEWS_TRANSLATE_GOOGLE"] = "Google로 번역",
        ["NEWS_TRANSLATING"] = "번역 중…",
        ["NEWS_SHOW_ORIGINAL"] = "원문",
        ["NEWS_TRANSLATE_UNAVAILABLE"] = "번역 불가",
        ["NEWS_NO_BODY"] = "(본문이 없습니다. 원문 보기를 눌러주세요.)",
        ["STATUS_INITIALIZING"] = "준비 중…",
        ["STATUS_LOADING"] = "불러오는 중",
        ["STATUS_COMPILING_SHADERS"] = "셰이더 컴파일 중",
        ["STATUS_ENUMERATING"] = "리소스 확인 중",
        ["WELCOME_BACK"] = "{0}님, 환영합니다",
        ["DIALOG_CONFIRM_TITLE"] = "확인",
        ["DIALOG_YES"] = "예",
        ["DIALOG_NO"] = "아니요",
        ["MENU_QUIT"] = "종료",
        ["CLOUD_PUSH_CONFIRM"] =
            "로컬 세이브를 클라우드에 올릴까요?\n클라우드 세이브를 덮어씁니다.",
        ["CLOUD_PULL_CONFIRM"] = "클라우드 세이브를 내려받을까요?\n로컬 세이브를 덮어씁니다.",
        ["CLOUD_PUSH_RUNNING"] = "세이브를 올리는 중…",
        ["CLOUD_PULL_RUNNING"] = "세이브를 내려받는 중…",
        ["CLOUD_PROGRESS"] = "{0}  {1} / {2}  ({3}%)",
        ["CLOUD_DONE"] = "완료됐습니다.",
        ["CLOUD_DONE_COUNTS"] = "완료 — 클라우드 {0}개 중 {1}개 처리, {2}개는 클라우드에 없음",
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
        ["VERSION_TITLE"] = "Version information",
        ["VERSION_LAUNCHER"] = "Launcher",
        ["VERSION_GAME_FILES"] = "Downloaded game",
        ["VERSION_GAME_DLL"] = "Loaded game DLL",
        ["VERSION_ENGINE"] = "Godot engine",
        ["VERSION_RUNTIME"] = ".NET runtime",
        ["VERSION_UNAVAILABLE"] = "Unavailable",
        ["VERSION_COPY"] = "Copy version information",
        ["BENCH_RESULTS_TAB"] = "Results",
        ["BENCH_CAPTURE_TAB"] = "Visual comparison",
        ["BENCH_SCENE_CARDS"] = "Cards",
        ["BENCH_SCENE_GEOMETRY"] = "Geometry",
        ["BENCH_SCENE_EFFECTS"] = "Screen effects",
        ["BENCH_FRAME_TIMES"] = "Frame mean / p95 / p99",
        ["BENCH_RESULTS_HELP"] =
            "Compare results within the same scene. CPU and GPU measure scene rendering, not full combat performance or battery consumption.",
        ["BENCH_NO_CAPTURE"] = "No comparison images saved yet.",
        ["BENCH_READY"] = "Start automatic comparison to apply options and restart as needed.",
        ["BENCH_COMPLETED"] = "Comparison completed. Your original settings are restored.",
        ["BENCH_CANCELLED"] = "Cancelled. Completed results were preserved.",
        ["BENCH_INTERRUPTED"] =
            "The previous test was interrupted. View completed results or start again.",
        ["BENCH_TITLE"] = "Rendering benchmark",
        ["BENCH_INFO"] =
            "Automatically compares options in fixed card, geometry and screen-effect scenes, including cold restarts for frame pacing. Leave the app untouched and do not resize the screen. Your settings are restored at the end. Results and comparison images remain available offline.",
        ["BENCH_START"] = "Start automatic comparison",
        ["BENCH_CANCEL"] = "Cancel",
        ["BENCH_CLOSE"] = "Back",
        ["BENCH_COPY"] = "Copy all results",
        ["BENCH_COPIED"] = "All results copied.",
        ["BENCH_NO_RESULT"] = "No saved results.",
        ["BENCH_ERROR"] = "Could not start benchmark.",
        ["BENCH_RESTART"] = "Restarting automatically for the next test…",
        ["BENCH_WARM"] = "Warming up",
        ["BENCH_MEASURE"] = "Measuring",
        ["GRAPHICS_PRESET"] = "Graphics preset",
        ["GRAPHICS_PRESET_QUALITY"] = "Quality",
        ["GRAPHICS_PRESET_BALANCED"] = "Balanced",
        ["GRAPHICS_PRESET_BATTERY"] = "Power saving",
        ["GRAPHICS_PRESET_CUSTOM"] = "Custom",
        ["GRAPHICS_PRESET_INFO"] =
            "Game default restores the original visual settings. Quality improves edge and texture quality. Balanced reduces resolution and some effects. Power saving lowers resolution and disables radial blur, screen distortion and background particles.\n\nFPS limit, performance overlay, shader warmup and frame pacing stay unchanged. Lower the FPS limit as well to draw fewer frames. Presets keep experimental direct card rendering off. Applies on the next game launch.",
        ["GRAPHICS_RESOLUTION_INFO"] =
            "Controls the internal rendering resolution. Lower values reduce the pixels processed by the GPU, but cards and text can look softer.\n\nLayout and touch coordinates stay unchanged. Applies on the next game launch.",
        ["GRAPHICS_MSAA_INFO"] =
            "Samples geometry edges to reduce jagged outlines. Higher values can increase GPU work and memory use; not every image or text label benefits.\n\nGame default follows the game setting. Applies on the next launch and schedules shader warmup when changed.",
        ["GRAPHICS_HDR_INFO"] =
            "Calculates 2D rendering with a wider color range. This can help some light and color effects, but may increase render-buffer memory and bandwidth use. It does not enable HDR output on the phone display.\n\nApplies on the next launch and schedules shader warmup when changed.",
        ["GRAPHICS_FILTER_INFO"] =
            "Controls how image pixels are sampled during scaling. Nearest preserves pixel edges; linear blends them. Mipmaps can reduce shimmering on small images; anisotropic filtering can sharpen angled textures.\n\nImages with their own filter override are unaffected. Applies on the next launch.",
        ["GRAPHICS_CARD_COMPOSITION_INFO"] =
            "Original keeps the game's portrait composition. Direct rendering is experimental and skips an intermediate composition step on ordinary cards. Cards that need a separate mask keep original composition.\n\nUse Original if artwork or effects look wrong. Applies on the next launch.",
        ["GRAPHICS_RADIAL_BLUR_INFO"] =
            "A screen blur used by certain effects. Low reduces its sample count; Off skips that blur and its screen copy. It only matters while the effect is active.\n\nApplies on the next game launch.",
        ["GRAPHICS_DISTORTION_INFO"] =
            "Displays selected effects that read and distort the screen. Off hides those effects and can reduce their rendering work. It does not disable all animations or camera shake.\n\nApplies on the next game launch.",
        ["GRAPHICS_PARTICLES_INFO"] =
            "Controls moving particles in combat backgrounds. Fewer particles reduce visual detail and can reduce CPU and GPU work. Off stops their emission and display.\n\nCard and character effects stay enabled. Applies on the next game launch.",
        ["GRAPHICS_WARMUP_INFO"] =
            "Prepares shaders during startup to reduce stutter when effects first appear. Initial preparation takes longer; completed warmup is skipped on later launches.\n\nOff skips pre-warming. The rebuild button below schedules a new warmup for the next launch.",
        ["GRAPHICS_PACING_INFO"] =
            "Controls how Android presents frames. Automatic FPS adjusts frame timing; Automatic FPS + pipeline also adjusts frame preparation. Keep selected FPS disables automatic timing adjustment.\n\nThis is separate from the FPS limit and does not guarantee a higher FPS or lower battery use. Changing it restarts the app when you press PLAY.",
        ["SETTING_FRAME_LIMIT_INFO"] =
            "Sets an upper limit on frames drawn per second. Lower values can reduce CPU and GPU work but make motion less smooth. Unlimited removes this cap; display refresh and frame pacing may still limit it.\n\nDevice performance and heat can keep FPS below the chosen limit. Applied when the game starts.",
        ["SETTING_FPS_OVERLAY_INFO"] =
            "Shows FPS and selected performance readings during play. Off stops both the overlay and its measurement work.\n\nThe FPS limit and graphics settings remain active.",
        ["SETTING_OVERLAY_CPU_INFO"] =
            "Shows the app's CPU usage in the performance overlay. Off stops sampling this reading. No sampling runs while the performance overlay itself is off.",
        ["SETTING_OVERLAY_GPU_INFO"] =
            "Shows GPU usage provided by the device. Off stops sampling this reading. Some devices do not expose it.",
        ["SETTING_OVERLAY_TEMP_INFO"] =
            "Shows temperature readings provided by the device. Off stops sampling this reading. Sensor availability and meaning vary by device.",
        ["LAUNCHER_TITLE"] = "Unofficial StS2 Launcher",
        ["UNOFFICIAL_NOTICE"] = "Not affiliated with or endorsed by Mega Crit or Valve.",
        ["MENU_PLAY"] = "PLAY",
        ["MENU_RETRY"] = "RETRY",
        ["MENU_NEWS"] = "News",
        ["MENU_SETTINGS"] = "Settings",
        ["MENU_GRAPHICS"] = "Graphics",
        ["MENU_GENERAL"] = "General",
        ["GRAPHICS_HELP"] =
            "Saved on this device. Applies when you next press PLAY; changing frame pacing restarts the app. Lower resolutions can blur text.",
        ["GRAPHICS_RESOLUTION"] = "Render Resolution",
        ["GRAPHICS_MSAA"] = "Edge Quality (MSAA)",
        ["GRAPHICS_HDR"] = "HDR 2D",
        ["GRAPHICS_FILTER"] = "Texture Filtering",
        ["GRAPHICS_CARD_COMPOSITION"] = "Card Portrait Composition",
        ["GRAPHICS_RADIAL_BLUR"] = "Radial Screen Blur",
        ["GRAPHICS_DISTORTION"] = "Screen Distortion",
        ["GRAPHICS_PARTICLES"] = "Background Particles",
        ["GRAPHICS_WARMUP"] = "Shader Warmup",
        ["GRAPHICS_PACING"] = "Frame Pacing · Restart",
        ["GRAPHICS_GAME_DEFAULT"] = "Game Default",
        ["GRAPHICS_NEAREST"] = "Nearest",
        ["GRAPHICS_LINEAR"] = "Linear",
        ["GRAPHICS_MIPMAP"] = "Linear + Mipmaps",
        ["GRAPHICS_ANISOTROPIC"] = "Mipmaps + Anisotropic",
        ["GRAPHICS_ORIGINAL"] = "Original",
        ["GRAPHICS_DIRECT"] = "Direct Draw (Experimental)",
        ["GRAPHICS_REDUCED"] = "Low",
        ["GRAPHICS_PACING_AUTO"] = "Auto FPS + Pipeline",
        ["GRAPHICS_PACING_AUTO_FPS"] = "Auto FPS",
        ["GRAPHICS_PACING_FIXED"] = "Keep Selected FPS",
        ["GRAPHICS_REBUILD_WARMUP"] = "Warm Up Shaders on Next PLAY",
        ["GRAPHICS_WARMUP_PENDING"] = "Shaders will warm up again on next PLAY.",
        ["GRAPHICS_WARMUP_DISABLED"] = "Enable warmup to compile shaders on next PLAY.",
        ["GRAPHICS_SAVE_FAILED"] = "Could not save settings. Check the console.",
        ["GRAPHICS_RESTART_PENDING"] = "Saved · PLAY will restart the app to apply this.",
        ["GRAPHICS_SAVED"] = "Saved · Applies on next PLAY.",
        ["MENU_CONSOLE"] = "Console",
        ["MENU_LEGAL"] = "Legal & Privacy",
        ["LEGAL_TITLE"] = "Legal & Privacy",
        ["LEGAL_LOAD_FAILED"] = "The bundled legal notice could not be loaded.",
        ["MENU_UPDATE_LAUNCHER"] = "UPDATE LAUNCHER",
        ["ACTION_CLOSE"] = "Close",
        ["ACTION_COPY_LOG"] = "Copy to clipboard",
        ["SETTING_LOCAL_BACKUP"] = "Local Backup",
        ["SETTING_AUTO_SYNC"] = "Auto Sync",
        ["SETTING_BETA_CHANNEL"] = "Beta Channel",
        ["SETTING_FPS_OVERLAY"] = "FPS Overlay",
        ["SETTING_FRAME_LIMIT"] = "Frame Limit (FPS)",
        ["FRAME_LIMIT_UNLIMITED"] = "Unlimited",
        ["SETTING_OVERLAY_CPU"] = "Overlay · CPU",
        ["SETTING_OVERLAY_GPU"] = "Overlay · GPU",
        ["SETTING_OVERLAY_TEMP"] = "Overlay · Temperature",
        ["SETTING_CLOUD_HEADER"] = "Cloud Saves",
        ["SETTING_UPLOAD_SAVES"] = "Upload",
        ["SETTING_DOWNLOAD_SAVES"] = "Download",
        ["SETTING_CHECK_UPDATES"] = "Check",
        ["SETTING_UPDATE_ROW"] = "Game updates",
        ["NEWS_HEADER"] = "Steam News",
        ["NEWS_LOADING"] = "Loading…",
        ["NEWS_BACK_TO_LIST"] = "← List",
        ["NEWS_OPEN_ORIGINAL"] = "Open original",
        ["NEWS_TRANSLATE"] = "Translate (Google if needed)",
        ["NEWS_TRANSLATE_GOOGLE"] = "Translate with Google",
        ["NEWS_TRANSLATING"] = "Translating…",
        ["NEWS_SHOW_ORIGINAL"] = "Original",
        ["NEWS_TRANSLATE_UNAVAILABLE"] = "Unavailable",
        ["NEWS_NO_BODY"] = "(No body text. Use Open original.)",
        ["STATUS_INITIALIZING"] = "Initializing…",
        ["STATUS_LOADING"] = "Loading",
        ["STATUS_COMPILING_SHADERS"] = "Compiling shaders",
        ["STATUS_ENUMERATING"] = "Enumerating resources",
        ["WELCOME_BACK"] = "Welcome back, {0}",
        ["DIALOG_CONFIRM_TITLE"] = "Confirm",
        ["DIALOG_YES"] = "Yes",
        ["DIALOG_NO"] = "No",
        ["MENU_QUIT"] = "Quit",
        ["CLOUD_PUSH_CONFIRM"] =
            "Upload local saves to the cloud?\nThis overwrites your cloud saves.",
        ["CLOUD_PULL_CONFIRM"] = "Download cloud saves?\nThis overwrites your local saves.",
        ["CLOUD_PUSH_RUNNING"] = "Uploading saves…",
        ["CLOUD_PULL_RUNNING"] = "Downloading saves…",
        ["CLOUD_PROGRESS"] = "{0}  {1} / {2}  ({3}%)",
        ["CLOUD_DONE"] = "Done.",
        ["CLOUD_DONE_COUNTS"] = "Done — {1} of {0} handled, {2} not in the cloud",
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
