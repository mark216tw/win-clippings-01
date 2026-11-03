namespace Clippings.Core.Input;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

public sealed record HotkeyGesture(HotkeyModifiers Modifiers, uint VirtualKey)
{
    private static readonly IReadOnlyDictionary<string, uint> NamedKeys = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
    {
        ["PrintScreen"] = 0x2C,
        ["PrtSc"] = 0x2C,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Space"] = 0x20,
        ["Tab"] = 0x09,
        ["Escape"] = 0x1B,
        ["Esc"] = 0x1B,
        ["Enter"] = 0x0D,
        ["Backspace"] = 0x08,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
    };

    private static readonly IReadOnlyDictionary<uint, string> KeyNames
        = NamedKeys
            .GroupBy(pair => pair.Value)
            .ToDictionary(group => group.Key, group => group.First().Key);

    public string DisplayName
    {
        get
        {
            var parts = new List<string>(4);
            if (Modifiers.HasFlag(HotkeyModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            {
                parts.Add("Alt");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            {
                parts.Add("Shift");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Win))
            {
                parts.Add("Win");
            }

            parts.Add(GetKeyName(VirtualKey));
            return string.Join('+', parts);
        }
    }

    public static bool TryParse(string? text, out HotkeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        uint? key = null;

        foreach (var rawPart in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (rawPart.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                case "ctl":
                    modifiers |= HotkeyModifiers.Control;
                    continue;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    continue;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    continue;
                case "win":
                case "windows":
                case "meta":
                    modifiers |= HotkeyModifiers.Win;
                    continue;
            }

            if (key is not null || !TryParseKey(rawPart, out var parsedKey))
            {
                return false;
            }

            key = parsedKey;
        }

        if (key is null || modifiers == HotkeyModifiers.None)
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key.Value);
        return true;
    }

    public static bool TryParseKey(string text, out uint virtualKey)
    {
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.Length == 1)
        {
            var c = char.ToUpperInvariant(text[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = c;
                return true;
            }
        }

        if (NamedKeys.TryGetValue(text, out var named))
        {
            virtualKey = named;
            return true;
        }

        if (text.Length is >= 2 and <= 3 && (text[0] is 'f' or 'F') && int.TryParse(text[1..], out var fn)
            && fn is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + fn - 1);
            return true;
        }

        return false;
    }

    public static string GetKeyName(uint virtualKey)
    {
        if (virtualKey is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return $"F{virtualKey - 0x70 + 1}";
        }

        return KeyNames.TryGetValue(virtualKey, out var name) ? name : $"VK_{virtualKey:X2}";
    }

    public override string ToString() => DisplayName;
}
