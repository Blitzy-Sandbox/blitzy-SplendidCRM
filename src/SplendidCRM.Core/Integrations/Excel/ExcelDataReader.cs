#nullable disable
using System;
using System.IO;
using System.Data;
namespace SplendidCRM.Excel
{
    /// <summary>Excel data reader utility for importing Excel files. Dormant stub — compiles but not activated.</summary>
    public class ExcelDataReader
    {
        public DataTable ReadExcelFile(Stream stream) { return new DataTable(); }
        public DataTable ReadExcelFile(string filePath) { using (var fs = File.OpenRead(filePath)) return ReadExcelFile(fs); }
    }
}
