using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CLodopfuncs
{
    class Program
    {
        static void Main(string[] args)
        {
            CLodop.CLodopfuncs.CLODOP.PRINT_INIT("123");
            CLodop.CLodopfuncs.CLODOP.ADD_PRINT_HTM("4%", "3%", "94%", "90%", "<html><body>Hello你好</body></html>");
            CLodop.CLodopfuncs.CLODOP.SET_PRINT_PAGESIZE("0", "0", "0", "A4");
            CLodop.CLodopfuncs.CLODOP.SET_PRINTER_INDEX("Microsoft Print to PDF");
            CLodop.CLodopfuncs.CLODOP.PRINT();

        }
    }
}
