using Steamworks;

namespace DesktopMaid
{
    /// <summary>Steam App / DLC ID 상수</summary>
    public static class SteamIds
    {
        public const uint APP_ID_DESKTOP_MAID_U32      = 3861360;
        public const uint DLC_ID_UNLIMITED_PRESETS_U32 = 3861380;

        // ──────────── AppId_t 래핑 ────────────
        public static readonly AppId_t APP_ID_DESKTOP_MAID
            = new AppId_t(APP_ID_DESKTOP_MAID_U32);

        public static readonly AppId_t DLC_ID_UNLIMITED_PRESETS
            = new AppId_t(DLC_ID_UNLIMITED_PRESETS_U32);
    }
}