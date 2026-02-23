#nullable disable
using System;
using System.Collections.Generic;
namespace SplendidCRM.FileBrowser
{
    /// <summary>File browser utility for managing uploaded files. Dormant stub.</summary>
    public class FileBrowserUtils
    {
        public List<string> ListFiles(string path) { return new List<string>(); }
        public byte[] ReadFile(string path) { return Array.Empty<byte>(); }
        public void WriteFile(string path, byte[] data) { }
        public void DeleteFile(string path) { }
    }
}
