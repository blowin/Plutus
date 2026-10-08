using System.Text;
using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain;

public interface IFileInfoDetailProvider
{
    IFileInfo CreateDirectory(string path);
    IFileInfo CreateFile(string path);
    void CreateDirectoryForFile(IFileInfo fileInfo);
    StreamWriter CreateWriterForFile(IFileInfo fileInfo, Encoding? encoding = null);
    IFileProvider CreateFileProvider(IFileInfo fileInfo);

    bool IsReparsePoint(IFileInfo fileInfo);
    string GetCurrentDirectory();
}
