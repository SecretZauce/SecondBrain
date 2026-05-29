using System;

namespace SecretZauce.SecondBrain.Editor
{
    public readonly struct SemanticVersion : IComparable<SemanticVersion>
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;

        SemanticVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public static bool TryParse(string input, out SemanticVersion result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var parts = input.Trim().Split('.');
            if (parts.Length < 2)
                return false;

            if (!int.TryParse(parts[0], out int major) ||
                !int.TryParse(parts[1], out int minor))
                return false;

            int patch = 0;
            if (parts.Length >= 3)
                int.TryParse(parts[2], out patch);

            result = new SemanticVersion(major, minor, patch);
            return true;
        }

        public int CompareTo(SemanticVersion other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            return Patch.CompareTo(other.Patch);
        }

        public static bool operator >(SemanticVersion a, SemanticVersion b)  => a.CompareTo(b) > 0;
        public static bool operator <(SemanticVersion a, SemanticVersion b)  => a.CompareTo(b) < 0;
        public static bool operator >=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) >= 0;
        public static bool operator <=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) <= 0;

        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}
