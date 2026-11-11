using System.Runtime.InteropServices;

namespace PiggyOcr.Platform;

public static class KeyboardState
{
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vk);

    public static uint CurrentModifiers()
    {
        uint modifiers = 0;
        if (IsDown(VK_CONTROL)) modifiers |= 0x0002;
        if (IsDown(VK_SHIFT)) modifiers |= 0x0004;
        if (IsDown(VK_MENU)) modifiers |= 0x0001;
        if (IsDown(VK_LWIN) || IsDown(VK_RWIN)) modifiers |= 0x0008;
        return modifiers;
    }

    private static bool IsDown(int vk) => (GetKeyState(vk) & 0x8000) != 0;
}
