using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDeezerStats.Application.Dtos.LastStream
{
    public class ListeningDto
    {
        public string Track { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string Duration {  get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}
