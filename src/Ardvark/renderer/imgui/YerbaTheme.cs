using System.Numerics;
using ImGuiNET;

namespace IMGUI
{
    // ────────────────────────────────────────────────────────────────────────
    //  YerbaTheme — faithful C# port of the Yerba menu's layout + theme
    //  constants (from Yerba/src/framework/config/layout.h + theme.h)
    // ────────────────────────────────────────────────────────────────────────
    public static class YerbaLayout
    {
        public const float WindowW = 700f;
        public const float WindowH = 560f;

        public const float OuterBorder = 0.5f;
        public const float IceBorder = 0.5f;
        public const float CornerR = 9f;

        public const float HeaderH = 45f;
        public const float PadX = 12f;

        public const float LogoSize = 42f;
        public const float LogoSepW = 2f;
        public const float LogoSepH = 20f;
        public const float LogoSepGap = 10f;

        public const float NavGap = 22f;
        public const float NavFont = 14f;

        public const float SearchW = 163f;
        public const float SearchH = 28f;
        public const float SearchRound = 4f;
        public const float SearchIconPad = 9f;
        public const float SearchOutline = 0.5f;
        public const float SearchFieldOutlineW = 1f;
        public const float FieldOutlineW = 0.5f;

        public const float OutlineOpacity = 0.5f;
        public const float ShellOuterOpacity = 0.85f;

        public const float SeparatorH = 2f;
        public const float SeparatorFadePower = 0.62f;
        public const int SeparatorSegments = 96;

        public const float DotSpacing = 10f;
        public const float DotRadius = 0.9f;

        public const float ContentTopOffset = 14f;
        public const float ContentPadX = 14f;
        public const float ContentPadBottom = 14f;
        public const float ColumnGap = 12f;

        public const float PanelRound = 7f;
        public const float PanelHeaderH = 29f;
        public const float PanelHeaderSep = 1f;
        public const float PanelPad = 10f;
        public const float PanelTitleFont = 14f;

        public const float SettingTopPad = 14f;
        public const float SettingLabelPad = 14f;
        public const float SettingControlPad = 14f;
        public const float SettingRowH = 32f;
        public const float SettingRowGap = 2f;
        public const float SettingRowFont = 15f;

        public const float CheckboxSize = 17f;
        public const float CheckboxRound = 3.5f;

        public const float KeybindW = 74f;
        public const float KeybindH = 22f;
        public const float KeybindRound = 6f;
        public const float KeybindIconW = 34f;
        public const float KeybindDivW = 1f;
        public const float KeybindKeyFont = 15f;
        public const float KeybindOutlineW = FieldOutlineW;
        public const float KeybindPressScale = 0.07f;
        public const float KeybindPressDur = 0.28f;

        public const float SearchTextPadLeft = 28f;
        public const float SearchFont = 14f;

        public const float ToggleOutlineW = 0.5f;

        public const float UnloadH = 36f;
        public const float UnloadRound = 5f;
        public const float UnloadTopGap = 10f;
        public const float ActionBtnGradientSpan = 1f;
        public const float ActionBtnHoverScale = 0.025f;
        public const float ActionBtnPressScale = 0.018f;
        public const float ActionBtnAnimSpeed = 14f;

        public const float ConfigPad = 12f;
        public const float ConfigLabelFont = 15f;
        public const float ConfigListGap = 8f;
        public const float ConfigListRound = 6f;
        public const float ConfigListOutline = 0.5f;
        public const float ConfigInputOutline = FieldOutlineW;
        public const float ConfigRowGap = 10f;
        public const float ConfigInputH = 32f;
        public const float ConfigInputRound = 6f;
        public const float ConfigInputPad = 12f;
        public const float ConfigCreateW = 74f;
        public const float ConfigCreateGap = 8f;
        public const float ConfigActionH = 38f;
        public const float ConfigActionGap = 8f;
        public const float ConfigBtnRound = 5f;
        public const float ConfigBtnFont = 15f;
        public const float ConfigListItemH = 28f;
        public const float ConfigListItemPad = 10f;

        public const float IconFontSize = 13f;

        public const float ClientW = WindowW + OuterBorder * 2f;
        public const float ClientH = WindowH + OuterBorder * 2f;
    }

    public static class YerbaColors
    {
        public static uint ShellBg = Col(11, 11, 13, 255);
        public static uint HeaderBgTop = Col(32, 32, 36, 255);
        public static uint HeaderBgBottom = Col(17, 17, 20, 255);
        public static uint BodyBg = Col(0x0A, 0x0A, 0x0A, 255);
        public static uint BodyDot = Col(48, 48, 54, 255);
        public static uint BorderBlack = Col(0, 0, 0, 255);
        public static uint Divider = Col(42, 42, 50, 255);

        public static uint TextActive = Col(255, 255, 255, 255);
        public static uint TextIdle = Col(118, 118, 128, 255);
        public static uint SearchBg = Col(17, 17, 17, 255);
        public static uint SearchIcon = Col(96, 96, 108, 255);
        public static uint SearchHint = Col(118, 118, 128, 255);
        public static uint SearchBorder = Col(0x52, 0x52, 0x58, 140);

        public static uint PanelInnerBg = Col(17, 17, 17, 255);
        public static uint PanelTitle = Col(255, 255, 255, 255);

        public static uint ToggleOnTop = Col(0x31, 0x71, 0x9B, 255);
        public static uint ToggleOnBottom = Col(0x1E, 0x52, 0x72, 255);
        public static uint ToggleOutline = Col(0x52, 0x52, 0x58, 140);

        public static uint KeybindBg = Col(0x1E, 0x1E, 0x22, 255);
        public static uint KeybindBgActive = Col(0x24, 0x28, 0x30, 255);
        public static uint KeybindOutline = Col(0x52, 0x52, 0x58, 110);
        public static uint KeybindOutlineActive = Col(0x58, 0x78, 0x92, 150);
        public static uint KeybindDivider = Col(0x42, 0x42, 0x48, 255);
        public static uint KeybindIcon = Col(0xA8, 0xA8, 0xB0, 255);
        public static uint KeybindWaitingText = Col(0xC8, 0xD8, 0xE8, 255);

        public static uint PanelHeaderLine = Col(0x31, 0x71, 0x9B, 255);

        public static uint ActionBtnTop = Col(0x1C, 0x1C, 0x1C, 255);
        public static uint ActionBtnBottom = Col(0x14, 0x14, 0x14, 255);
        public static uint ActionBtnHighlight = Col(0x24, 0x24, 0x24, 255);
        public static uint ActionBtnHoverTop = Col(0x3A, 0x3A, 0x3A, 255);
        public static uint ActionBtnHoverBottom = Col(0x2A, 0x2A, 0x2A, 255);
        public static uint ActionBtnHoverOutline = Col(0x4A, 0x4A, 0x50, 255);

        public static uint ConfigListBg = Col(0x00, 0x00, 0x00, 255);
        public static uint ConfigListBorder = Col(0x3A, 0x3A, 0x3E, 255);
        public static uint ConfigInputBg = Col(0x11, 0x11, 0x11, 255);
        public static uint ConfigInputBorder = Col(0x52, 0x52, 0x58, 110);
        public static uint ConfigListSelBg = Col(0x1E, 0x1E, 0x22, 255);

        public static uint UnloadTop = ActionBtnTop;
        public static uint UnloadBottom = ActionBtnBottom;

        public static uint IceBlue = Col(118, 208, 242, 255);

        public static uint SepGrad0 = Col(0x13, 0x1A, 0x1E, 255);
        public static uint SepGrad1 = Col(0x23, 0x31, 0x39, 255);
        public static uint SepGrad2 = Col(0x33, 0x46, 0x52, 255);
        public static uint SepGrad3 = Col(0x1C, 0x34, 0x43, 255);
        public static uint SepGrad4 = Col(0x0D, 0x24, 0x32, 255);
        public static uint SepGrad5 = Col(0x09, 0x13, 0x18, 255);
        public static uint LogoBlueHi = Col(170, 215, 255, 255);
        public static uint LogoBlueLo = Col(70, 130, 210, 255);

        public static uint Col(int r, int g, int b, int a)
            => ImGui.ColorConvertFloat4ToU32(new Vector4(r / 255f, g / 255f, b / 255f, a / 255f));

        public static uint LerpColor(uint a, uint b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            int ar = (int)((a >> 0) & 0xFF);
            int ag = (int)((a >> 8) & 0xFF);
            int ab = (int)((a >> 16) & 0xFF);
            int aa = (int)((a >> 24) & 0xFF);
            int br = (int)((b >> 0) & 0xFF);
            int bg = (int)((b >> 8) & 0xFF);
            int bb = (int)((b >> 16) & 0xFF);
            int ba = (int)((b >> 24) & 0xFF);
            return Col(
                ar + (int)((br - ar) * t),
                ag + (int)((bg - ag) * t),
                ab + (int)((bb - ab) * t),
                aa + (int)((ba - aa) * t));
        }

        public static uint WithAlpha(uint col, float alpha)
        {
            alpha = Math.Clamp(alpha, 0f, 1f);
            return Col(
                (int)((col >> 0) & 0xFF),
                (int)((col >> 8) & 0xFF),
                (int)((col >> 16) & 0xFF),
                (int)(255f * alpha));
        }
    }
}