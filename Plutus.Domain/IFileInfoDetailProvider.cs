using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain;

public interface IFileInfoDetailProvider
{
    bool IsReparsePoint(IFileInfo fileInfo);
}

public sealed class PhysicianPlutusFileInfo : IFileInfoDetailProvider
{
    public bool IsReparsePoint(IFileInfo fileInfo)
    {
        if (fileInfo.PhysicalPath is null)
        {
            return false;
        }

        try
        {
            var fileAttributes = File.GetAttributes(fileInfo.PhysicalPath);
            return (fileAttributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch
        {
            return false;
        }
    }
}
