using System.Globalization;

namespace GuvenOn.Core;

/// <summary>
/// Minimal semantic-version implementation used by the update pipeline.
/// Accepts an optional leading "v", ignores build metadata and compares
/// pre-release identifiers according to the SemVer 2.0.0 ordering rules.
/// </summary>
public readonly struct SemVer : IComparable<SemVer>, IEquatable<SemVer>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? PreRelease { get; }

    public SemVer(int major, int minor, int patch, string? preRelease = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = string.IsNullOrWhiteSpace(preRelease) ? null : preRelease;
    }

    public static bool TryParse(string? value, out SemVer version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        var plus = text.IndexOf('+');
        if (plus >= 0)
        {
            var metadata = text[(plus + 1)..];
            if (!IsValidIdentifierList(metadata, allowLeadingZeros: true))
            {
                return false;
            }

            text = text[..plus];
        }

        string? preRelease = null;
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            preRelease = text[(dash + 1)..];
            if (!IsValidIdentifierList(preRelease, allowLeadingZeros: false))
            {
                return false;
            }

            text = text[..dash];
        }

        var parts = text.Split('.');
        if (parts.Length != 3 ||
            !TryParseCore(parts[0], out var major) ||
            !TryParseCore(parts[1], out var minor) ||
            !TryParseCore(parts[2], out var patch))
        {
            return false;
        }

        version = new SemVer(major, minor, patch, preRelease);
        return true;
    }

    private static bool TryParseCore(string value, out int number)
    {
        number = 0;
        if (value.Length == 0 || (value.Length > 1 && value[0] == '0'))
        {
            return false;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private static bool IsValidIdentifierList(string value, bool allowLeadingZeros)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var identifier in value.Split('.'))
        {
            if (identifier.Length == 0)
            {
                return false;
            }

            var numeric = true;
            foreach (var character in identifier)
            {
                if (character is >= '0' and <= '9')
                {
                    continue;
                }

                numeric = false;
                var letterOrDash =
                    (character is >= 'A' and <= 'Z') ||
                    (character is >= 'a' and <= 'z') ||
                    character == '-';
                if (!letterOrDash)
                {
                    return false;
                }
            }

            if (!allowLeadingZeros && numeric && identifier.Length > 1 && identifier[0] == '0')
            {
                return false;
            }
        }

        return true;
    }

    public int CompareTo(SemVer other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        if (PreRelease is null && other.PreRelease is null)
        {
            return 0;
        }

        if (PreRelease is null)
        {
            return 1;
        }

        if (other.PreRelease is null)
        {
            return -1;
        }

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    private static int ComparePreRelease(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        var length = Math.Min(leftParts.Length, rightParts.Length);

        for (var i = 0; i < length; i++)
        {
            var a = leftParts[i];
            var b = rightParts[i];
            var aNumeric = IsNumericIdentifier(a);
            var bNumeric = IsNumericIdentifier(b);

            int result;
            if (aNumeric && bNumeric)
            {
                // Compare numeric identifiers as arbitrarily large numbers without
                // converting to int, so huge identifiers cannot overflow or order wrongly.
                var aValue = TrimLeadingZeros(a);
                var bValue = TrimLeadingZeros(b);
                result = aValue.Length != bValue.Length
                    ? aValue.Length.CompareTo(bValue.Length)
                    : string.CompareOrdinal(aValue, bValue);
            }
            else if (aNumeric)
            {
                result = -1;
            }
            else if (bNumeric)
            {
                result = 1;
            }
            else
            {
                result = string.Compare(a, b, StringComparison.Ordinal);
            }

            if (result != 0)
            {
                return result;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static bool IsNumericIdentifier(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static string TrimLeadingZeros(string value)
    {
        var trimmed = value.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    public bool Equals(SemVer other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is SemVer other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);

    public override string ToString() =>
        PreRelease is null
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}-{PreRelease}";

    public static bool operator >(SemVer left, SemVer right) => left.CompareTo(right) > 0;
    public static bool operator <(SemVer left, SemVer right) => left.CompareTo(right) < 0;
    public static bool operator >=(SemVer left, SemVer right) => left.CompareTo(right) >= 0;
    public static bool operator <=(SemVer left, SemVer right) => left.CompareTo(right) <= 0;
    public static bool operator ==(SemVer left, SemVer right) => left.Equals(right);
    public static bool operator !=(SemVer left, SemVer right) => !left.Equals(right);
}
