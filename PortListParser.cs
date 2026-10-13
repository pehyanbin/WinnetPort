using System.Globalization;

namespace PortScanner.WinForms;

// Converts a comma-separated set of ports and short inclusive ranges into unique ports.
internal static class PortListParser
{
    public static bool TryParse(
        string? input, int maxPorts, out int[] ports, out string error)
    {
        ports = Array.Empty<int>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Enter one or more TCP ports, such as 22, 80, 443 or 8000-8005.";
            return false;
        }

        var selection = new SortedSet<int>();
        foreach (string item in input.Split(','))
        {
            string token = item.Trim();
            string[] parts = token.Split('-');
            if (parts.Length is < 1 or > 2 ||
                !TryPort(parts[0].Trim(), out int start) ||
                (parts.Length == 2 && !TryPort(parts[1].Trim(), out _)))
            {
                error = $"Invalid port entry: '{token}'. Use numbers from 1 to 65535, separated by commas.";
                return false;
            }

            int end = start;
            if (parts.Length == 2)
            {
                TryPort(parts[1].Trim(), out end);
                if (end < start)
                {
                    error = $"Invalid range '{token}': the final port must not be smaller than the first.";
                    return false;
                }
            }

            if (end - start + 1 > maxPorts)
            {
                error = $"Select at most {maxPorts:N0} unique ports in one scan.";
                return false;
            }

            for (int port = start; port <= end; port++)
            {
                selection.Add(port);
                if (selection.Count > maxPorts)
                {
                    error = $"Select at most {maxPorts:N0} unique ports in one scan.";
                    return false;
                }
            }
        }

        ports = selection.ToArray();
        return true;
    }

    private static bool TryPort(string input, out int port) =>
        int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out port) &&
        port is >= 1 and <= 65535;
}
