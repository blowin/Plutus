using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Plutus.Domain;

namespace Plutus.Infrastructure;

public sealed class PhysicianPlutusFileInfo : IFileInfoDetailProvider
{
    public IFileInfo CreateDirectory(string path) => new PhysicalDirectoryInfo(new DirectoryInfo(path));
    public IFileInfo CreateFile(string path) => new PhysicalFileInfo(new FileInfo(path));
    public void CreateDirectoryForFile(IFileInfo fileInfo)
    {
        var ioFileInfo = new FileInfo(fileInfo.PhysicalPath ?? throw new Exception("Unable to get physical file path"));
        ioFileInfo.Directory?.Create();
    }

    public StreamWriter CreateWriterForFile(IFileInfo fileInfo, Encoding? encoding = null)
    {
        var fileInfoPhysicalPath = fileInfo.PhysicalPath ?? throw new Exception("Unable to get physical file path");
        return new StreamWriter(fileInfoPhysicalPath, append: false, encoding: encoding);
    }

    public IFileProvider CreateFileProvider(IFileInfo fileInfo) => new PhysicalFileProvider(fileInfo.PhysicalPath ?? throw new ArgumentException("Unable to determine PhysicalPath"), ExclusionFilters.None);

    public bool IsReparsePoint(IFileInfo fileInfo)
    {
        if (fileInfo.PhysicalPath is null)
        {
            return false;
        }

        try
        {
            var fileAttributes = File.GetAttributes(fileInfo.PhysicalPath);
            return (fileAttributes & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }
}
