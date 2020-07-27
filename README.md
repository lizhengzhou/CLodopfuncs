# CLodopfuncs

## dependency

<package id="Newtonsoft.Json" version="12.0.3" targetFramework="net35" />
<package id="SuperSocket.ClientEngine.Core" version="0.10.0" targetFramework="net35" />
<package id="WebSocket4Net" version="0.15.2" targetFramework="net35" />

## Get Start

1.Copy CLodopfuncs.cs into your Project

2.Add some Line

CLodop.CLodopfuncs.CLODOP.PRINT_INIT("123");
CLodop.CLodopfuncs.CLODOP.ADD_PRINT_HTM("4%", "3%", "94%", "90%", "<html><body>Hello你好</body></html>");
CLodop.CLodopfuncs.CLODOP.SET_PRINT_PAGESIZE("0", "0", "0", "A4");
CLodop.CLodopfuncs.CLODOP.SET_PRINTER_INDEX("Microsoft Print to PDF");
CLodop.CLodopfuncs.CLODOP.PRINT();

3.Run
