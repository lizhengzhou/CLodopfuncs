using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CLodop
{
    public class CLodopfuncs
    {

        public static string Host = "localhost";
        public static int port = 8000;

        public readonly string VERSION = "6.5.0.2";
        public readonly string IVERSION = "6502";
        public readonly string CVERSION = "4.0.8.8";

        string DelimChar = "\f\f";
        string strWebPageID = "7BCAAAz";


        static readonly CLodopfuncs _CLODOP = null;

        WebSocket4Net.WebSocket webskt = null;

        static CLodopfuncs()
        {
            _CLODOP = new CLodopfuncs();
        }
        private CLodopfuncs()
        {
            this.GetCLodopPrinters();
            this.SET_LICENSES("上海汽车信息产业投资有限公司", "1A1683BD13ED8A56138F8BF8328862FF");

            this.DoInit();
            this.OpenWebSocket();
        }

        public static CLodopfuncs CLODOP
        {
            get { return _CLODOP; }
        }

        Dictionary<string, object> Printers = null;
        readonly Dictionary<string, object> ItemDatas = new Dictionary<string, object>();
        readonly Dictionary<string, string> PageData = new Dictionary<string, string>();
        readonly Dictionary<string, object> defStyleJson = new Dictionary<string, object>();
        readonly Dictionary<string, string> PageDataEx = new Dictionary<string, string>();
        readonly Dictionary<string, string> ItemCNameStyles = new Dictionary<string, string>();

        string strTaskID = "";

        int iBaseTask = 0;
        bool blIslocal = true;
        bool blWorking = false;
        string blTmpSelectedIndex = null;
        bool SocketOpened = false;
        bool NoClearAfterPrint = false;
        bool blNormalItemAdded = false;
        string Result = null;
        int? OBO_Mode = 1;
        bool blOneByone = false;
        string altMessageNoReadWriteFile = "不能远程读写文件!";
        string altMessageSomeWindowExist = "有窗口已打开，先关闭它(持续如此时请刷新页面)!";
        string altMessageBusy = "上一个请求正忙，请稍后再试！";


        string GetTaskID()
        {
            if (string.IsNullOrEmpty(this.strTaskID))
            {
                this.strTaskID = DateTime.Now.ToString("HHmmss") + "_" + (++this.iBaseTask);
            }
            return this.strWebPageID + this.strTaskID;
        }

        void DoInit()
        {
            this.strTaskID = "";
            if (this.NoClearAfterPrint) return;
            this.ItemDatas.Clear();
            this.ItemDatas.Add("count", 0);
            this.PageData.Clear(); ;
            this.ItemCNameStyles.Clear();
            this.defStyleJson.Clear();
            this.defStyleJson.Add("beginpage", 0);
            this.defStyleJson.Add("beginpagea", 0);
            this.blNormalItemAdded = false;
        }

        void OpenWebSocket()
        {
            try
            {
                if (this.webskt == null || this.webskt.State == WebSocket4Net.WebSocketState.Closed)
                {
                    this.webskt = new WebSocket4Net.WebSocket($"ws://{Host}:{port}/c_webskt/");
                    this.webskt.Opened += (object sender, EventArgs e) =>
                    {
                        this.SocketOpened = true;
                    };
                    this.webskt.MessageReceived += (object sender, WebSocket4Net.MessageReceivedEventArgs e) =>
                    {
                        this.blOneByone = false;
                        var strResult = e.Message;
                        this.Result = strResult;
                        try
                        {
                            string strFTaskID = null;
                            var iPos = strResult.IndexOf("=");
                            if (iPos >= 0 && iPos < 30)
                            {
                                strFTaskID = strResult.Substring(0, iPos);
                                strResult = strResult.Substring(iPos + 1);
                            }
                            if (strFTaskID.IndexOf("ErrorMS") > -1)
                            {
                                return;
                            }
                            if (strFTaskID.IndexOf("BroadcastMS") > -1)
                            {
                                return;
                            }
                        }
                        catch { }
                    };
                    this.webskt.Closed += (object sender, EventArgs e) =>
                    {
                        Thread.Sleep(2000);
                        this.OpenWebSocket();
                    };
                    this.webskt.Error += (object sender, SuperSocket.ClientEngine.ErrorEventArgs e) =>
                    {
                    };

                    this.webskt.Open();
                }
            }
            catch (Exception err)
            {
                this.webskt = null;
                if (err.Message.IndexOf("SecurityError") > -1)
                    throw err;
                else
                {
                    Thread.Sleep(2000);
                    this.OpenWebSocket();
                }
            }
        }

        bool wsSend(string strData, bool blReTry = false)
        {
            if (this.webskt != null && this.webskt.State == WebSocket4Net.WebSocketState.Open)
            {
                this.Result = null;
                this.webskt.Send(strData);
                return true;
            }
            else
            {
                if (!blReTry)
                {
                    Thread.Sleep(600);
                    this.wsSend(strData, true);
                }
                else
                {
                    this.OpenWebSocket();
                    Thread.Sleep(600);
                    this.wsSend(strData, false);
                }
            }
            return false;
        }

        string FORMAT(string oType, string oValue)
        {
            if (this.blWorking)
            {
                throw new ApplicationException(this.altMessageBusy);
            }
            string tResult = null;
            if (!string.IsNullOrEmpty(oType) && !string.IsNullOrEmpty(oValue))
            {
                if (Regex.Replace(oType, @"^\s +|\s +$", "").ToLower().IndexOf("time:") == 0)
                {
                    oType = Regex.Replace(oType, @"^\s +|\s +$", "").Substring(5);
                    if (oValue.ToLower().IndexOf("now") > -1) oValue = DateTime.Now.ToString();
                    if (oValue.ToLower().IndexOf("date") > -1) oValue = DateTime.Now.ToString();
                    if (oValue.ToLower().IndexOf("time") > -1) oValue = DateTime.Now.ToString();
                    var TypeYMD = "ymd";
                    if (oValue.ToLower().IndexOf("ymd") > -1) { TypeYMD = "ymd"; oValue = oValue.Substring(3); }
                    if (oValue.ToLower().IndexOf("dmy") > -1) { TypeYMD = "dmy"; oValue = oValue.Substring(3); }
                    if (oValue.ToLower().IndexOf("mdy") > -1) { TypeYMD = "mdy"; oValue = oValue.Substring(3); }
                    oValue = Regex.Replace(oValue, @"[^ ] *\+[^ ] *", "");
                    oValue = Regex.Replace(oValue, @"\(.*\) ", "");
                    oValue = Regex.Replace(oValue, @" 星期日 | 星期一 | 星期二 | 星期三 | 星期四 | 星期五 | 星期六 ", "");
                    oValue = oValue = Regex.Replace(oValue, @"[A - Za - z] + day | Mon | Tue | Wed | Thu | Fri | Sat | Sun ", "");
                    var aMonth = 0;
                    var exp = new Regex("Oct[A-Za-z]*|十月|10月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 10; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("Nov[A-Za-z]*|十一月|11月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 11; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("Dec[A-Za-z]*|十二月|12月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 12; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("Jan[A-Za-z]*|一月|01月|1月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 1; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("Feb[A-Za-z]*|二月|02月|2月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 2; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("Mar[A-Za-z]*|三月|03月|3月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 3; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("Apr[A-Za-z]*|四月|04月|4月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 4; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("May[A-Za-z]*|五月|05月|5月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 5; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("Jun[A-Za-z]*|六月|06月|6月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 6; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("Jul[A-Za-z]*|七月|07月|7月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 7; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("Aug[A-Za-z]*|八月|08月|8月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 8; oValue = exp.Replace(oValue, ""); }
                    exp = new Regex("Sep[A-Za-z]*|九月|09月|9月", RegexOptions.ExplicitCapture); if (exp.IsMatch(oValue)) { aMonth = 9; oValue = exp.Replace(oValue, ""); }
                    oValue = Regex.Replace(oValue, @" 日 | 秒 ", "");
                    oValue = Regex.Replace(oValue, @" 时 | 分 ", "");
                    var subTime = Regex.Match(oValue, @"\d +:\d +:\d +")?.Value;
                    if (subTime == null) subTime = "";
                    oValue = Regex.Replace(oValue, @" \d +:\d +:\d +", "") + subTime;
                    var dValue = DateTime.Now;
                    var iYear = 0; var iMonth = 0; var iDate = 0; var iHour = 0; var iMinutes = 0; var iSecond = 0;
                    var tmpValue = oValue; var sValue = "";
                    var MC1 = 0; var MC2 = 0; var MC3 = 0;
                    sValue = Regex.Match(tmpValue, @"\d +")?.Value; if (sValue != null) { MC1 = int.Parse("" + sValue[0]); tmpValue = Regex.Replace(oValue, @"\d +", ""); }
                    sValue = Regex.Match(tmpValue, @"\d +")?.Value; if (sValue != null) { MC2 = int.Parse("" + sValue[0]); tmpValue = Regex.Replace(oValue, @"\d +", ""); }
                    if (aMonth <= 0) { sValue = Regex.Match(tmpValue, @"\d +")?.Value; if (sValue != null) { MC3 = int.Parse("" + sValue[0]); tmpValue = Regex.Replace(oValue, @"\d +", ""); } }
                    if (aMonth > 0) { iMonth = aMonth; if (MC2 <= 31) { iYear = MC1; iDate = MC2; } else { iYear = MC2; iDate = MC1; } }
                    else
                        if (TypeYMD == "dmy") { iDate = MC1; iMonth = MC2; iYear = MC3; }
                    else
                            if (TypeYMD == "mdy") { iMonth = MC1; iDate = MC2; iYear = MC3; }
                    else
                    {
                        iYear = MC1; iMonth = MC2; iDate = MC3;
                        if (MC3 > 31) { iYear = MC3; iMonth = MC1; iDate = MC2; if (MC1 > 12) { iDate = MC1; iMonth = MC2; }; } else { if (MC2 > 12) { iYear = MC2; iMonth = MC1; } }
                    }
                    sValue = Regex.Match(tmpValue, @"\d +")?.Value; if (sValue != null) { iHour = int.Parse("" + sValue[0]); tmpValue = Regex.Replace(oValue, @"\d +", ""); }
                    sValue = Regex.Match(tmpValue, @"\d +")?.Value; if (sValue != null) { iMinutes = int.Parse("" + sValue[0]); tmpValue = Regex.Replace(oValue, @"\d +", ""); }
                    sValue = Regex.Match(tmpValue, @"\d +")?.Value; if (sValue != null) { iSecond = int.Parse("" + sValue[0]); tmpValue = Regex.Replace(oValue, @"\d +", ""); }
                    if (oType.ToLower() == "isvalidformat")
                        oValue = (iYear > 0 && iMonth > 0 && iMonth <= 12 && iDate > 0 && iDate <= 31).ToString();
                    else
                    {
                        if (("" + iYear).Length < 4) iYear = iYear + 2000;
                        dValue = new DateTime(iYear, iMonth, iDate, iHour, iMinutes, iSecond);

                        var iDay = dValue.DayOfWeek;
                        if (oType.ToLower() == "weekindex")
                            oValue = iDay.ToString();
                        else
                            if (oType.ToLower() == "floatvalue")
                            oValue = dValue.ToFileTime().ToString();
                        else
                        {
                            var sWeek = "";
                            switch ((int)iDay) { case 0: sWeek = "日"; break; case 1: sWeek = "一"; break; case 2: sWeek = "二"; break; case 3: sWeek = "三"; break; case 4: sWeek = "四"; break; case 5: sWeek = "五"; break; case 6: sWeek = "六"; break; }
                            oValue = Regex.Replace(oValue, @" dddd ", "星期" + sWeek, RegexOptions.ExplicitCapture | RegexOptions.Multiline);
                            if (Regex.IsMatch(@" (y +) ", oValue))
                            {
                                var match1 = Regex.Match(oValue, " (y +) ").Value;
                                oValue = oValue.Replace(match1, (iYear + "").Substring(4 - match1.Length));
                            }
                            if (Regex.IsMatch(@" (m +:)", oValue))
                            {
                                var match1 = Regex.Match(oValue, " (m +:)").Value;
                                oValue = oValue.Replace(match1, ("00" + iMinutes + ":").Substring(("00" + iMinutes + ":").Length - match1.Length));
                            }
                            if (Regex.IsMatch(@" (M +) ", oValue))
                            {
                                var match1 = Regex.Match(oValue, " (M +) ").Value;
                                var dsWidth = ("" + iMonth).Length > match1.Length ? ("" + iMonth).Length : match1.Length;
                                oValue = oValue.Replace(match1, ("00" + iMonth).Substring(("00" + iMonth).Length - dsWidth));
                            }
                            if (Regex.IsMatch(@" (d +) ", oValue))
                            {
                                var match1 = Regex.Match(oValue, " (d +) ").Value;
                                var dsWidth = ("" + iDate).Length > match1.Length ? ("" + iDate).Length : match1.Length;
                                oValue = oValue.Replace(match1, ("00" + iDate).Substring(("00" + iDate).Length - dsWidth));
                            }
                            if (Regex.IsMatch(@" (H +) ", oValue))
                            {
                                var match1 = Regex.Match(oValue, " (H +) ").Value;
                                oValue.Replace(match1, ("00" + iHour).Substring(("00" + iHour).Length - match1.Length));
                            }
                            if (Regex.IsMatch(@" (n +) ", oValue))
                            {
                                var match1 = Regex.Match(oValue, " (n +) ").Value;
                                oValue = oValue.Replace(match1, ("00" + iMinutes).Substring(("00" + iMinutes).Length - match1.Length));
                            }
                            if (Regex.IsMatch(@" (s +) ", oValue))
                            {
                                var match1 = Regex.Match(oValue, " (s +) ").Value;
                                oValue = oValue.Replace(match1, ("00" + iSecond).Substring(("00" + iSecond).Length - match1.Length));
                            }
                        }
                    }
                    return oValue;
                }
                else
                    if (this.blIslocal || oType.IndexOf("FILE:") < 0)
                {
                    this.PageData["format_type"] = oType;
                    this.PageData["format_value"] = oValue;
                    if (this.DoPostDatas("format") == true)
                    {
                        tResult = this.GetTaskID();
                    }
                }
                else
                {
                    throw new ApplicationException(this.altMessageNoReadWriteFile);
                }
            };
            this.DoInit();
            this.blWorking = false;
            return tResult;
        }

        public bool PRINT_INIT(string strPrintTask)
        {
            return this.PRINT_INITA(null, null, null, null, strPrintTask);
        }

        bool PRINT_INITA(string Top, string Left, string Width, string Height, string strPrintTask)
        {
            if (string.IsNullOrEmpty(Top)) Top = "";
            if (string.IsNullOrEmpty(Left)) Left = "";
            if (string.IsNullOrEmpty(Width)) Width = "";
            if (string.IsNullOrEmpty(Height)) Height = "";
            if (string.IsNullOrEmpty(strPrintTask)) strPrintTask = "";
            this.NoClearAfterPrint = false;
            this.DoInit();
            this.PageData["top"] = Top;
            this.PageData["left"] = Left;
            this.PageData["width"] = Width;
            this.PageData["height"] = Height;
            this.PageData["printtask"] = strPrintTask;
            return true;
        }

        string SET_PRINT_MODE(string strModeType, string ModeValue)
        {
            if (string.IsNullOrEmpty(strModeType)) strModeType = "";
            if (string.IsNullOrEmpty(ModeValue)) ModeValue = "";
            if (strModeType == "") return null;
            strModeType = strModeType.ToLower();
            this.PageData[strModeType] = ModeValue;
            if (strModeType == "noclear_after_print") this.NoClearAfterPrint = Convert.ToBoolean(ModeValue);
            if (strModeType.IndexOf("window_def") > -1 || strModeType.IndexOf("control_printer") > -1)
            {
                string tResult = null;
                if (this.DoPostDatas("onlysetprint") == true)
                {
                    tResult = this.GetTaskID();
                }
                this.DoInit();
                this.blWorking = false;
                return tResult;
            }
            return null;
        }

        public bool ADD_PRINT_HTM(string top, string left, string width, string height, string strHTML)
        {
            return this.AddItemArray(4, top, left, width, height, strHTML);
        }

        public bool ADD_PRINT_TABLE(string top, string left, string width, string height, string strHTML)
        {
            return this.AddItemArray(6, top, left, width, height, strHTML);
        }
        public string PREVIEW()
        {
            if (this.blWorking) { throw new ApplicationException(this.altMessageBusy); }
            string tResult = null;
            if ((this.blIslocal) && (!string.IsNullOrEmpty(this.PageData["printersubid"])))
            {
                if (this.DoPostDatas("preview") == true)
                {
                    this.Result = null;
                    tResult = this.GetTaskID();
                }
            }
            else
            {
                throw new ApplicationException("不支持!");
            }
            this.DoInit();
            this.blWorking = false;
            return tResult;
        }

        public string PRINT()
        {
            if (this.blWorking) { throw new ApplicationException(this.altMessageBusy); }
            string tResult = null;
            if (this.DoPostDatas("print") == true)
                tResult = this.GetTaskID();
            this.DoInit();
            this.blWorking = false;
            return tResult;
        }

        public bool SET_PRINTER_INDEX(string strName, string strKeyModeName = null)
        {
            if (this.Printers == null) return false;
            else
            {
                if (string.IsNullOrEmpty(strKeyModeName)) strKeyModeName = "printerindex";
                if (strName == null) strName = "";
                strName = Regex.Replace(strName, @"^\s +|\s +$", "");
                var iPos = strName.IndexOf(",");
                var strNameOrNO = strName;
                if (iPos > -1) strNameOrNO = strName.Substring(0, iPos);
                if (strNameOrNO == "-1")
                {
                    this.PageData[strKeyModeName] = this.Printers["default"].ToString();
                    if (iPos > -1) this.PageData["printersubid"] = strName.Substring(iPos + 1);
                    return true;
                }
                else
                {
                    var list = this.Printers["list"] as List<Dictionary<string, object>>;
                    for (var vNO = 0; vNO < list.Count; vNO++)
                    {
                        var strPrinterName = (list[vNO] as Dictionary<string, object>)["name"].ToString();
                        if (string.IsNullOrEmpty(strPrinterName)) continue;
                        if ((Regex.Replace(strPrinterName, @"\\", "") == Regex.Replace(strNameOrNO, @"\\", "")) || (vNO.ToString() == strNameOrNO))
                        {
                            this.PageData[strKeyModeName] = vNO.ToString();
                            if (iPos > -1) this.PageData["printersubid"] = strName.Substring(iPos + 1);
                            return true;
                        }
                    }
                    return false;
                }
            }
        }
        public bool SET_PRINTER_INDEXA(string strName)
        {
            return this.SET_PRINTER_INDEX(strName, "printerindexa");
        }
        public void SET_PRINT_PAGESIZE(string intOrient, string PageWidth, string PageHeight, string strPageName)
        {
            if (!string.IsNullOrEmpty(intOrient)) this.PageData["orient"] = intOrient;
            if (!string.IsNullOrEmpty(PageWidth)) this.PageData["pagewidth"] = PageWidth;
            if (!string.IsNullOrEmpty(PageHeight)) this.PageData["pageheight"] = PageHeight;
            if (!string.IsNullOrEmpty(strPageName)) this.PageData["pagename"] = strPageName;
        }
        public bool SET_PRINT_COPIES(string intCopies)
        {
            if (!string.IsNullOrEmpty(intCopies))
            {
                this.PageData["printcopies"] = intCopies;
                return true;
            }
            return false;
        }
        public void SET_LICENSES(string strCompanyName, string strLicense, string strLicenseA = null, string strLicenseB = null)
        {
            if ((strCompanyName == "THIRD LICENSE") && (strLicense == ""))
            {
                if (!string.IsNullOrEmpty(strLicenseA)) this.PageDataEx["licensec"] = strLicenseA;
                if (!string.IsNullOrEmpty(strLicenseB)) this.PageDataEx["licensed"] = strLicenseB;
            }
            else if ((strCompanyName == "LICENSE TETCODE") && (strLicense == "") && (strLicenseB == ""))
            {
                if (!string.IsNullOrEmpty(strLicenseA)) this.PageDataEx["Licensetetcode"] = strLicenseA;
            }
            else
            {
                if (!string.IsNullOrEmpty(strCompanyName)) this.PageDataEx["companyname"] = strCompanyName;
                if (!string.IsNullOrEmpty(strLicense)) this.PageDataEx["license"] = strLicense;
                if (!string.IsNullOrEmpty(strLicenseA)) this.PageDataEx["licensea"] = strLicenseA;
                if (!string.IsNullOrEmpty(strLicenseB)) this.PageDataEx["licenseb"] = strLicenseB;
            }
        }
        bool AddItemArray(int type, string top, string left, string width, string height, string strContent, string itemname = null, string ShapeType = null,
            string intPenStyle = null, string intPenWidth = null, string intColor = null, string isLinePosition = null, string BarType = null,
            string strChartTypess = null)
        {
            if (type <= 0 || string.IsNullOrEmpty(left) || string.IsNullOrEmpty(width)
                || string.IsNullOrEmpty(height) || string.IsNullOrEmpty(strContent))
            {
                return false;
            }
            var sCount = (int)this.ItemDatas["count"];
            sCount++;
            var oneItem = new Dictionary<string, object>();

            foreach (var vstyle in this.defStyleJson.Keys)
            {
                oneItem[vstyle] = this.defStyleJson[vstyle];
            }
            oneItem["type"] = type;
            oneItem["top"] = top;
            oneItem["left"] = left;
            oneItem["width"] = width;
            oneItem["height"] = height;
            if (strContent != null)
            {
                if (strContent.IndexOf(this.DelimChar) > -1)
                    oneItem["content"] = Regex.Replace(strContent, this.DelimChar, "");
                else
                    oneItem["content"] = strContent;
            }
            if (!string.IsNullOrEmpty(itemname)) oneItem["itemname"] = itemname + "";
            if ((string.IsNullOrEmpty(ShapeType))) oneItem["shapetype"] = ShapeType;
            if (!string.IsNullOrEmpty(intPenStyle)) oneItem["penstyle"] = intPenStyle;
            if (!string.IsNullOrEmpty(intPenWidth)) oneItem["penwidth"] = intPenWidth;
            if (!string.IsNullOrEmpty(intColor)) oneItem["fontcolor"] = intColor;
            if (!string.IsNullOrEmpty(isLinePosition)) oneItem["lineposition"] = "1";
            if (!string.IsNullOrEmpty(BarType)) oneItem["fontname"] = BarType;
            if (!string.IsNullOrEmpty(strChartTypess)) oneItem["charttypess"] = strChartTypess;

            oneItem["beginpage"] = this.defStyleJson["beginpage"];
            oneItem["beginpagea"] = this.defStyleJson["beginpagea"];
            this.ItemDatas["count"] = sCount;
            this.ItemDatas[sCount.ToString()] = oneItem;
            this.blNormalItemAdded = true;
            return true;
        }

        string createPostDataString(string afterPostAction)
        {

            var strData = "act=" + afterPostAction + this.DelimChar;
            strData = strData + "browseurl=" + "" + this.DelimChar; //window.location.href
            foreach (var vMode in this.PageDataEx.Keys)
            {
                strData = strData + vMode + "=" + this.PageDataEx[vMode] + this.DelimChar;
            }
            var PrintModeNamess = "";
            foreach (var vMode in this.PageData.Keys)
            {
                strData = strData + vMode + "=" + this.PageData[vMode] + this.DelimChar;
                if (vMode != "top" && vMode != "left" && vMode != "width" && vMode != "height" && vMode != "printtask" && vMode != "printerindex" && vMode != "printerindexa" && vMode != "printersubid" && vMode != "orient" && vMode != "pagewidth" && vMode != "pageheight" && vMode != "pagename" && vMode != "printcopies" && vMode != "setup_bkimg")
                    PrintModeNamess = PrintModeNamess + ";" + vMode;
            }
            if (PrintModeNamess != "")
                strData = strData + "printmodenames=" + PrintModeNamess + this.DelimChar;
            var StyleClassNamess = "";
            foreach (var vClassStyle in this.ItemCNameStyles.Keys)
            {
                strData = strData + vClassStyle + "=" + this.ItemCNameStyles[vClassStyle] + this.DelimChar;
                StyleClassNamess = StyleClassNamess + ";" + vClassStyle;
            }
            if (StyleClassNamess != "")
                strData = strData + "printstyleclassnames=" + StyleClassNamess + this.DelimChar;
            strData = strData + "itemcount=" + this.ItemDatas["count"] + this.DelimChar;
            foreach (var vItemNO in this.ItemDatas.Keys.Where(x => x != "count"))
            {
                var ItemStyless = "";
                foreach (var vItemxx in (this.ItemDatas[vItemNO] as Dictionary<string, object>).Keys)
                {
                    if (vItemxx != "beginpage" && vItemxx != "beginpagea" && vItemxx != "type" && vItemxx != "top" && vItemxx != "left" && vItemxx != "width" && vItemxx != "height")
                        ItemStyless = ItemStyless + ";" + vItemxx;
                }
                strData = strData + vItemNO + "_itemstylenames" + "=" + ItemStyless + this.DelimChar;
                foreach (var vItemxx in (this.ItemDatas[vItemNO] as Dictionary<string, object>).Keys)
                {
                    strData = strData + vItemNO + "_" + vItemxx + "=" + (this.ItemDatas[vItemNO] as Dictionary<string, object>)[vItemxx] + this.DelimChar;
                }
            }
            return strData;
        }

        bool wsDoPostDatas(string afterPostAction)
        {
            var strData = "charset=丂" + this.DelimChar;
            strData = strData + "tid=" + this.GetTaskID() + this.DelimChar;
            strData = strData + this.createPostDataString(afterPostAction);
            return this.wsSend("post:" + strData);
        }

        bool DoPostDatas(string afterPostAction)
        {
            if (this.OBO_Mode.HasValue && this.blOneByone)
            {
                throw new ApplicationException(this.altMessageSomeWindowExist);
            }
            this.blWorking = true;
            if (this.blTmpSelectedIndex != null)
                this.SET_PRINTER_INDEX(this.blTmpSelectedIndex);
            return this.wsDoPostDatas(afterPostAction);
        }

        void GetCLodopPrinters()
        {
            string jsUrl = $"http://{Host}:{port}/CLodopfuncs.js";
            try
            {
                string jsCLodopfuncs = GetJsContent(jsUrl);

                this.strWebPageID = (Regex.Match(jsCLodopfuncs, @"(?<=strWebPageID:).+(?=,strTaskID)")?.Value ?? "").Replace("\"","");
                
                string strPrinters = Regex.Match(jsCLodopfuncs, @"(?<=Printers:).+(?=,)")?.Value ?? "";

                JObject objPrinters = JObject.Parse(strPrinters);

                this.Printers = new Dictionary<string, object>();

                this.Printers.Add("default", objPrinters.Value<string>("default"));

                List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
                foreach (JObject objPrinter in objPrinters["list"].Children())
                {
                    Dictionary<string, object> printer = new Dictionary<string, object>();
                    foreach (var prop in objPrinter)
                    {
                        if (prop.Key == "pagelist")
                        {
                            List<object> pagelist = new List<object>();
                            foreach (JObject objpage in objPrinter["pagelist"].Children())
                            {
                                Dictionary<string, string> page = new Dictionary<string, string>();
                                foreach (var pageprop in objpage)
                                {
                                    page.Add(pageprop.Key, pageprop.Value.ToString());
                                }
                                pagelist.Add(page);
                            }
                            printer.Add("pagelist", pagelist);
                        }
                        else if (prop.Key == "subdevlist")
                        {
                            //todo
                        }
                        else
                        {
                            printer.Add(prop.Key, prop.Value.ToString());
                        }
                    }
                    list.Add(printer);
                }
                this.Printers.Add("list", list);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"{jsUrl}获取解析失败:{ex.Message}");
            }
        }

        string GetJsContent(string url)
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.Method = "GET";

            string Content = "";
            Encoding encoding = Encoding.GetEncoding("UTF-8");
            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            if (httpWebResponse.StatusCode == HttpStatusCode.OK)
            {
                Stream stream = httpWebResponse.GetResponseStream();
                StreamReader streamReader = new StreamReader(stream, encoding);
                Content = streamReader.ReadToEnd();
                streamReader.Close();
            }
            httpWebResponse.Close();

            return Content;
        }


    }


}
