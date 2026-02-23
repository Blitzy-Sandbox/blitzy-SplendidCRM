#nullable disable
using System;
namespace SplendidCRM.FileBrowser
{
    /// <summary>File connector for managing file system operations. Dormant stub.</summary>
    public class FileConnector
    {
        public string BasePath { get; set; }
        public FileConnector(string basePath) { BasePath = basePath; }
        public bool FileExists(string relativePath) { return false; }
        public string[] GetDirectories(string relativePath) { return Array.Empty<string>(); }
        public string[] GetFiles(string relativePath) { return Array.Empty<string>(); }
    }
}
