namespace BanDoSu.Core;

public sealed class ExportPublisher
{
    private readonly StudioSettings _settings;
    private readonly ProjectFiles _projects;

    public ExportPublisher(StudioSettings settings)
    {
        _settings = settings;
        _projects = new ProjectFiles(settings);
    }

    public string Publish(string project)
    {
        ProjectFiles.ValidateRoots(_settings);
        ProjectFiles.ValidateName(project);
        var source = _projects.FileInProject(project, "out");
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("Chưa có thư mục kết quả: " + source);
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        if (files.Length == 0) throw new IOException("Thư mục kết quả chưa có tệp để xuất.");

        var root = Path.Combine(Path.GetFullPath(_settings.ExportRoot), project);
        Directory.CreateDirectory(root);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var destination = Path.Combine(root, stamp);
        for (var n = 1; Directory.Exists(destination); n++)
            destination = Path.Combine(root, stamp + "-" + n);
        Directory.CreateDirectory(destination);

        try
        {
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(source, file);
                var target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: false);
                if (new FileInfo(file).Length != new FileInfo(target).Length)
                    throw new IOException("Không thể xác minh tệp đã xuất: " + relative);
            }
            return destination;
        }
        catch
        {
            // Only delete the newly created export folder; source project is never touched.
            Directory.Delete(destination, recursive: true);
            throw;
        }
    }
}
