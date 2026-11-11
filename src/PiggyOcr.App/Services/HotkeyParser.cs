namespace PiggyOcr.App.Services;

public static class HotkeyParser
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    public static bool TryParse(string text, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;

        foreach (string part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "win": modifiers |= ModWin; break;
                case "ctrl":
                case "control": modifiers |= ModControl; break;
                case "shift": modifiers |= ModShift; break;
                case "alt": modifiers |= ModAlt; break;
                default:
                    if (!TryParseKey(part, out key)) return false;
                    break;
            }
        }

        return key != 0;
    }

    public static string Format(uint modifiers, uint key)
    {
        var parts = new List<string>(4);
        if ((modifiers & ModWin) != 0) parts.Add("Win");
        if ((modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((modifiers & ModShift) != 0) parts.Add("Shift");
        parts.Add(KeyName(key));
        return string.Join("+", parts);
    }

    public static string KeyName(uint key)
    {
        if (key is >= (uint)'A' and <= (uint)'Z' || key is >= (uint)'0' and <= (uint)'9')
            return ((char)key).ToString();
        if (key is >= 0x70 and <= 0x87)
            return $"F{key - 0x70 + 1}";
        return key.ToString();
    }

    private static bool TryParseKey(string text, out uint key)
    {
        key = 0;
        if (text.Length == 1)
        {
            char c = char.ToUpperInvariant(text[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                key = c;
                return true;
            }
        }

        if (text.Length is >= 2 and <= 3 && (text[0] == 'F' || text[0] == 'f')
            && int.TryParse(text[1..], out int number) && number is >= 1 and <= 24)
        {
            key = (uint)(0x70 + number - 1);
            return true;
        }

        return false;
    }
}
