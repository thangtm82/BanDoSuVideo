namespace BanDoSu.Core;

/// <summary>
/// Resolves the npm JavaScript entrypoints from the configured Node installation.
/// Never executes npm.cmd/npx.cmd shims discovered through the current directory,
/// studio/node_modules/.bin, or a modified process PATH.
/// </summary>
public sealed class NodePackageTools
{
    public NodePackageTools(string nodeExecutable, string studioRoot)
    {
        NodeExecutable = nodeExecutable;
        StudioRoot = Path.GetFullPath(studioRoot);
    }

    public string NodeExecutable { get; }
    public string StudioRoot { get; }

    public string? FindNodeExecutable()
    {
        if (Path.IsPathRooted(NodeExecutable))
            return File.Exists(NodeExecutable) ? Path.GetFullPath(NodeExecutable) : null;

        // In normal installations NodeExecutable is "node" (or "node.exe").
        // Avoid implicit resolution from WorkingDirectory by searching PATH explicitly.
        if (NodeExecutable.Contains(Path.DirectorySeparatorChar) ||
            NodeExecutable.Contains(Path.AltDirectorySeparatorChar))
            return null;

        var name = NodeExecutable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? NodeExecutable : NodeExecutable + ".exe";
        var searchPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var entry in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = entry.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(directory)) continue;
            try
            {
                var full = Path.GetFullPath(directory);
                // Do not use programs installed inside the project as global tools.
                if (full.Equals(StudioRoot, StringComparison.OrdinalIgnoreCase) ||
                    full.StartsWith(StudioRoot.TrimEnd(Path.DirectorySeparatorChar) +
                        Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    continue;
                var candidate = Path.Combine(full, name);
                if (File.Exists(candidate)) return candidate;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException
                or PathTooLongException) { }
        }
        return null;
    }

    public string ResolveNpmCli(bool npx = false)
    {
        var node = FindNodeExecutable();
        if (node is null)
            throw new FileNotFoundException("Không tìm thấy Node.js từ đường dẫn cấu hình hoặc PATH. " +
                "Hãy kiểm tra Node.js trong tab Cài đặt và khởi động lại ứng dụng sau khi cài bằng WinGet.");

        var cliName = npx ? "npx-cli.js" : "npm-cli.js";
        var cli = Path.Combine(Path.GetDirectoryName(node)!, "node_modules", "npm", "bin", cliName);
        if (!File.Exists(cli))
            throw new FileNotFoundException(
                "Node.js đã được tìm thấy nhưng thiếu " + cliName + " trong bản cài npm đi kèm: " + cli +
                ". Hãy sửa chữa/cài lại Node.js LTS. Không tạo node_modules/npm trong thư mục studio.",
                cli);
        return cli;
    }

    public string ResolvePlaywrightCli()
    {
        var cli = Path.Combine(StudioRoot, "node_modules", "playwright", "cli.js");
        if (!File.Exists(cli))
            throw new FileNotFoundException(
                "Chưa tìm thấy Playwright của H2 Studio tại " + cli +
                ". Hãy cài 'Các gói npm của engine' trước khi cài Chromium.",
                cli);
        return cli;
    }
}
