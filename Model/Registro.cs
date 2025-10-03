using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DafTools.Model
{
    public class Registro
    {
        public string Prefeitura { get; set; } = string.Empty;
        public int Ano { get; set; }
        public int Mes { get; set; }
        public string Fundo { get; set; } = string.Empty;
        public string Debito { get; set; } = string.Empty;
        public string Credito { get; set; } = string.Empty;
    }

}
