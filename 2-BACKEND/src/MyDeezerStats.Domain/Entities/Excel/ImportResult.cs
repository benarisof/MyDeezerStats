using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDeezerStats.Domain.Entities.Excel
{
    public class ImportResult
    {
        public int TotalRows { get; set; }
        public int RowsProcessed { get; set; }
        public int RowsImported { get; set; }
        public int RowsSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
        public TimeSpan ProcessingTime { get; set; }
        public double SuccessRate => TotalRows > 0 ? (double)RowsImported / TotalRows * 100 : 0;
    }
}
