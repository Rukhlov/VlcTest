using System;
using System.Collections.Generic;

using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace VlcContracts
{
    public class NativeMethods
    {
        public const int SWP_NOOWNERZORDER = 0x200;
        public const int SWP_NOREDRAW = 0x8;
        public const int SWP_NOZORDER = 0x4;
        public const int SWP_SHOWWINDOW = 0x0040;
        public const int WS_EX_MDICHILD = 0x40;
        public const int SWP_FRAMECHANGED = 0x20;
        public const int SWP_NOACTIVATE = 0x10;
        public const int SWP_ASYNCWINDOWPOS = 0x4000;
        public const int SWP_NOMOVE = 0x2;
        public const int SWP_NOSIZE = 0x1;
        public const int GWL_STYLE = (-16);
        public const int WS_VISIBLE = 0x10000000;
        public const int WS_CHILD = 0x40000000;

        public const int WS_MAXIMIZEBOX = 0x10000;
        public const int WS_MINIMIZEBOX = 0x20000;

        [DllImport("user32.dll")]
        extern public static int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        extern public static int SetWindowLong(IntPtr hwnd, int index, int value);

        public static void HideMinimizeAndMaximizeButtons(IntPtr hwnd)
        {
            var currentStyle = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, (currentStyle & ~WS_MAXIMIZEBOX & ~WS_MINIMIZEBOX));
        }


        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int FreeConsole();


        [DllImport("user32.dll", SetLastError = true)]
        public static extern long SetParent(IntPtr hWndChild, IntPtr hWndNewParent);


        [DllImport("user32.dll", EntryPoint = "SetWindowLongA", SetLastError = true)]
        public static extern int SetWindowLongA([System.Runtime.InteropServices.InAttribute()] System.IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern long SetWindowPos(IntPtr hwnd, long hWndInsertAfter, long x, long y, long cx, long cy, long wFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);




        [DllImport("kernel32.dll")]
        public static extern void ZeroMemory(IntPtr dst, ulong length);


    }

    public class YoutubeUrlResolver
    {

        public static List<List<string>> Extractor(string url)
        {

            var html_content = "";
            using (var client = new WebClient())

            {
                client.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_10_1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2227.1 Safari/537.36");
                html_content += client.DownloadString(url);

            }

            var Regex1 = new Regex(@"url=(.*?tags=\\u0026)", RegexOptions.Multiline);
            var matched = Regex1.Match(html_content);
            var download_infos = new List<List<string>>();
            foreach (var matched_group in matched.Groups)
            {
                var urls = Regex.Split(WebUtility.UrlDecode(matched_group.ToString().Replace("\\u0026", " &")), ",?url=");

                foreach (var vid_url in urls.Skip(1))
                {
                    var download_url = vid_url.Split(' ')[0].Split(',')[0].ToString();
                    Console.WriteLine(download_url);

                    // for quality info of the video
                    var Regex2 = new Regex("(quality=|quality_label=)(.*?)(,|&| |\")");
                    var QualityInfo = Regex2.Match(vid_url);
                    var quality = QualityInfo.Groups[2].ToString(); //quality_info
                    download_infos.Add((new List<string> { download_url, quality })); //contains url and resolution

                }
            }

            //System.Threading.Thread.Sleep(10000);
            return download_infos;

        }

    }

    //public static class ExtensionsHelper
    //{
    //    public static System.Drawing.Imaging.PixelFormat ToGdiPixelFormat(this PixelFormat pixelFormat)
    //    {
    //        var gdiFormat = System.Drawing.Imaging.PixelFormat.Undefined;
    //        if (pixelFormat == PixelFormats.Bgra32)
    //        {
    //            gdiFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
    //        }
    //        else if (pixelFormat == PixelFormats.Bgr32)
    //        {
    //            gdiFormat = System.Drawing.Imaging.PixelFormat.Format32bppRgb;
    //        }
    //        else if (pixelFormat == PixelFormats.Bgr24)
    //        {
    //            gdiFormat = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
    //        }
    //        return gdiFormat;
    //    }

    //    private static PixelFormat FromGdiPixelFormat(System.Drawing.Imaging.PixelFormat gdiFormat)
    //    {
    //        if (gdiFormat == System.Drawing.Imaging.PixelFormat.Format24bppRgb)
    //        {
    //            return PixelFormats.Bgr24;
    //        }
    //        else if (gdiFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
    //        {
    //            return PixelFormats.Bgra32;
    //        }
    //        else if (gdiFormat == System.Drawing.Imaging.PixelFormat.Format32bppRgb)
    //        {
    //            return PixelFormats.Bgr32;

    //        }
    //        return new PixelFormat();
    //    }
    //}
}
