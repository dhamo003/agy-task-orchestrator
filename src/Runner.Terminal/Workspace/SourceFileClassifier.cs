using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AntigravityTaskRunner.Configuration;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Terminal.Workspace;

/// <summary>
/// Classifies workspace files (source / documentation / ignored) and produces
/// comment- and whitespace-normalized content hashes so that formatting-only edits
/// can be distinguished from real implementation changes.
/// </summary>
public sealed class SourceFileClassifier
{
    private static readonly Regex BlockComments = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex LineComments = new(@"//[^\r\n]*", RegexOptions.Compiled);
    private static readonly Regex HashComments = new(@"(?m)^\s*#[^\r\n]*", RegexOptions.Compiled);
    private static readonly Regex XmlComments = new(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private static readonly HashSet<string> CStyleExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".cs", ".ts", ".tsx", ".js", ".jsx", ".java", ".cpp", ".c", ".h", ".go", ".rs", ".sql" };

    private static readonly HashSet<string> HashCommentExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".py", ".sh", ".yaml", ".yml", ".rb" };

    private static readonly HashSet<string> XmlStyleExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".csproj", ".props", ".targets", ".xml", ".razor", ".cshtml", ".html" };

    private readonly VerificationOptions _options;

    public SourceFileClassifier(IOptions<RunnerOptions> options)
    {
        _options = options.Value.Verification;
    }

    /// <summary>True when the file should be excluded from verification entirely (binaries, cache, logs, VCS…).</summary>
    public bool IsIgnored(string relativePath)
    {
        if (_options.IgnoredExtensions.Contains(GetExtension(relativePath), StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = "/" + relativePath.Replace('\\', '/').TrimStart('/');
        foreach (var fragment in _options.IgnoredPathFragments)
        {
            var f = fragment.Replace('\\', '/');
            if (normalized.Contains(f, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the file counts as an implementation/source file.</summary>
    public bool IsSource(string relativePath) =>
        _options.SourceExtensions.Contains(GetExtension(relativePath), StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the file is documentation-only (changes never count as implementation).</summary>
    public bool IsDocumentation(string relativePath) =>
        _options.DocumentationExtensions.Contains(GetExtension(relativePath), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Computes the SHA-256 of the comment/whitespace-normalized content, or null when
    /// the file is not a source file.
    /// </summary>
    public string? ComputeNormalizedHash(string relativePath, string content)
    {
        if (!IsSource(relativePath))
        {
            return null;
        }

        var normalized = Normalize(GetExtension(relativePath), content);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    /// <summary>
    /// Strips comments (per language family) and collapses all whitespace, so two
    /// contents that differ only in comments/formatting normalize identically.
    /// </summary>
    public static string Normalize(string extension, string content)
    {
        if (CStyleExtensions.Contains(extension))
        {
            content = BlockComments.Replace(content, " ");
            content = LineComments.Replace(content, " ");
        }
        else if (HashCommentExtensions.Contains(extension))
        {
            content = HashComments.Replace(content, " ");
        }
        else if (XmlStyleExtensions.Contains(extension))
        {
            content = XmlComments.Replace(content, " ");
        }

        return Whitespace.Replace(content, "");
    }

    private static string GetExtension(string path) => Path.GetExtension(path) ?? string.Empty;
}
