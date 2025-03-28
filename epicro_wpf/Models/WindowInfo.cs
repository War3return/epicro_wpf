using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace epicro_wpf.Models
{
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public int ProcessId { get; set; }

        public override string ToString() => $"{Title} ({ProcessId})";
    }
}
