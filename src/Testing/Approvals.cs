using System.Runtime.CompilerServices;
using System.Text;

namespace YAF.Testing;

/// <summary>
/// Compares a text with a committed approved file next to the calling test, for contracts that change only on purpose: error catalogues,
/// field ids, inventories and schemas (ADR-0018). A difference writes a received file beside the approved one and fails listing the
/// added and removed lines.
/// </summary>
/// <remarks>
/// The approved file is found through <see cref="CallerFilePathAttribute"/>, so the test project must compile with real source paths:
/// set <c>&lt;DeterministicSourcePaths&gt;false&lt;/DeterministicSourcePaths&gt;</c> in it, because a CI build maps the repository root
/// to <c>/_/</c> otherwise. When a difference is intended, read the listed lines, replace the approved file with the received file and
/// commit it with the change. Never commit a received file.
/// </remarks>
/// <example>
/// <code>
/// var codes = string.Join('\n', catalog.Select(error => error.Code));
///
/// Approvals.Verify(codes, "errors");
/// </code>
/// </example>
public static class Approvals
{
    /// <summary>
    /// Asserts that <paramref name="actualText"/> equals the file <c>&lt;name&gt;.approved.txt</c> in the directory of the calling file,
    /// line by line. Line ends do not matter (<c>\r\n</c>, <c>\r</c> and <c>\n</c> are the same), and neither does a line end after the
    /// last line.
    /// </summary>
    /// <param name="actualText">The text the test produced.</param>
    /// <param name="name">The name of the approved file without <c>.approved.txt</c>, for example <c>errors</c>.</param>
    /// <param name="callerFilePath">Supplied by the compiler; do not pass it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="actualText"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    /// <exception cref="YafAssertionException">
    /// The directory of the calling file does not exist (the message names <c>DeterministicSourcePaths</c>); or the approved file is
    /// missing or differs, in which case <c>&lt;name&gt;.received.txt</c> is written with <paramref name="actualText"/> and the message lists
    /// the added lines with <c>+</c> and the removed lines with <c>-</c>. When the texts are equal, a stale received file is deleted.
    /// </exception>
    public static void Verify(string actualText, string name, [CallerFilePath] string callerFilePath = "")
    {
        ArgumentNullException.ThrowIfNull(actualText);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var directory = Path.GetDirectoryName(callerFilePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new YafAssertionException(
                $"The directory of the calling file '{callerFilePath}' does not exist, so '{name}.approved.txt' cannot be found. A CI build "
                + "maps source paths to '/_/': set <DeterministicSourcePaths>false</DeterministicSourcePaths> in the test project.");
        }

        var approvedPath = Path.Combine(directory, $"{name}.approved.txt");
        var receivedPath = Path.Combine(directory, $"{name}.received.txt");
        var approvedExists = File.Exists(approvedPath);
        var approved = approvedExists ? SplitLines(File.ReadAllText(approvedPath)) : [];
        var actual = SplitLines(actualText);

        if (approvedExists && approved.SequenceEqual(actual, StringComparer.Ordinal))
        {
            File.Delete(receivedPath);
            return;
        }

        File.WriteAllText(receivedPath, actualText);

        var message = new StringBuilder(approvedExists
            ? $"The text differs from '{approvedPath}'."
            : $"The approved file '{approvedPath}' does not exist.");
        message.Append(" The text was written to '").Append(receivedPath)
            .Append("'. If the difference is intended, replace the approved file with it.").Append('\n');
        foreach (var line in Diff(approved, actual))
        {
            message.Append('\n').Append(line);
        }

        throw new YafAssertionException(message.ToString());
    }

    private static string[] SplitLines(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    // A line diff over the longest common subsequence, in the order of the texts: '-' for a line only in the approved text, '+' for a
    // line only in the actual one. Common lines are left out.
    private static List<string> Diff(string[] approved, string[] actual)
    {
        var common = new int[approved.Length + 1][];
        common[approved.Length] = new int[actual.Length + 1];
        for (var i = approved.Length - 1; i >= 0; i--)
        {
            common[i] = new int[actual.Length + 1];
            for (var j = actual.Length - 1; j >= 0; j--)
            {
                common[i][j] = string.Equals(approved[i], actual[j], StringComparison.Ordinal)
                    ? common[i + 1][j + 1] + 1
                    : Math.Max(common[i + 1][j], common[i][j + 1]);
            }
        }

        var lines = new List<string>();
        int a = 0, b = 0;
        while (a < approved.Length || b < actual.Length)
        {
            if (a < approved.Length && b < actual.Length && string.Equals(approved[a], actual[b], StringComparison.Ordinal))
            {
                a++;
                b++;
            }
            else if (b < actual.Length && (a == approved.Length || common[a][b + 1] >= common[a + 1][b]))
            {
                lines.Add($"+ {actual[b++]}");
            }
            else
            {
                lines.Add($"- {approved[a++]}");
            }
        }

        return lines;
    }
}
