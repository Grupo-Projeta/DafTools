using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DafTools.Model
{
    public class DafCsvScheme
    {
        public string Name { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public string Fund { get; set; } = string.Empty;
        public string Debt { get; set; } = string.Empty;
        public string Credit { get; set; } = string.Empty;
    }

}
