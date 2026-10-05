using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain;

public interface IFileInfoDetailProvider
{
    FileAttributes GetFileAttributes(IFileInfo fileInfo);
}

public sealed class PhysicianPlutusFileInfo(IFileInfo root) : IFileInfoDetailProvider
{
    public FileAttributes GetFileAttributes(IFileInfo fileInfo)
    {
        return fileInfo.PhysicalPath != null ? File.GetAttributes(fileInfo.PhysicalPath) : FileAttributes.None;
    }
}
