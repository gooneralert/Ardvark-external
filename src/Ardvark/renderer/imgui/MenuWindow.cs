using ClickableTransparentOverlay;

namespace IMGUI
{
    // Disabled: a second ClickableTransparentOverlay instance in the same
    // process crashes. The menu + ESP now share ONE overlay (Program).
    public class MenuWindow : Overlay
    {
        protected override void Render()
        {
            // Unused — the single overlay (Program) renders menu + ESP.
        }
    }
}