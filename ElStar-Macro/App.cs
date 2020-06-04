using AForge.Imaging;
using Binarysharp.MemoryManagement;
using ElStar_Macro.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static ElStar_Macro.WinUtils;
using MemorySharpNative = Binarysharp.MemoryManagement.Native;

namespace ElStar_Macro
{
    public class App
    {
        public App()
        {
        }
        private string X2Path { get; set; }
        private IntPtr X2WindowHandle;
        private Rectangle X2WindowRect { get { try { AppUtils.GetWindowRect(this.X2WindowHandle, out var rect); return rect; } catch { } return Rectangle.Empty; } }
        private List<Tuple<ConsoleKey, Bitmap>> ImageKeys;
        private MemorySharp client;
        public async Task StartAsync()
        {
            var process = this.getX2Process();
            this.X2WindowHandle = process.MainWindowHandle;
            this.X2Path = process.StartInfo.FileName;
            this.client = new MemorySharp(process.Id);
            this.ImageKeys = new List<Tuple<ConsoleKey, Bitmap>>()
            {
                new Tuple<ConsoleKey, Bitmap>(ConsoleKey.LeftArrow, (Bitmap)Properties.Resources.MouseLeft.Clone()),
                new Tuple<ConsoleKey, Bitmap>(ConsoleKey.UpArrow, (Bitmap)Properties.Resources.MouseUp.Clone()),
                new Tuple<ConsoleKey, Bitmap>(ConsoleKey.RightArrow, (Bitmap)Properties.Resources.MouseRight.Clone()),
                new Tuple<ConsoleKey, Bitmap>(ConsoleKey.DownArrow, (Bitmap)Properties.Resources.MouseDown.Clone()),
                new Tuple<ConsoleKey, Bitmap>(ConsoleKey.X, (Bitmap)Properties.Resources.KeyX.Clone()),
                new Tuple<ConsoleKey, Bitmap>(ConsoleKey.Z, (Bitmap)Properties.Resources.KeyZ.Clone()),
                new Tuple<ConsoleKey, Bitmap>(ConsoleKey.C, (Bitmap)Properties.Resources.KeyC.Clone())
            };
            Console.WriteLine("Recommended Window Size: 1024x768");
            Console.WriteLine($"Found Window, PID: {process.Id}, HNWD: {X2WindowHandle.ToString("x2")}, {X2WindowRect.Width}x{X2WindowRect.Height}");
            await this.StartMacro(X2WindowHandle);
            await Task.Delay(-1);
        }
        private async Task StartMacro(IntPtr windowHandle)
        {
            float similarity = 0.95f;
            //Properties.Resources.StageActiveAlternative.SetResolution(stageActiveCheckResolution, stageActiveCheckResolution);
            var initialCheck = new ExhaustiveTemplateMatching(0.97f);
            var af = new ExhaustiveTemplateMatching(similarity);
            Bitmap scr;
            while(true)
            {
                using (scr = ScreenCapture.CaptureWindow(windowHandle))
                {
                    //scr.SetResolution(stageActiveCheckResolution, stageActiveCheckResolution);
                    if (initialCheck.ProcessImage(scr, Properties.Resources.StageActiveAlternative,
                        new Rectangle(
                            475,
                            115,
                            78,
                            86))
                        .Length == 0)
                    {
                        Console.WriteLine($"Active Stage not found");
                    }
                    else
                    {
                        var sw = new Stopwatch();
                        sw.Start();
                        var foundKeys = new List<Tuple<ConsoleKey, Rectangle>>();
                        Bitmap screen = scr.Clone(new Rectangle(275, 189, 488, 39), PixelFormat.Format24bppRgb);
                        screen.SetResolution(50.0f, 50.0f);
                        for (int i = 0; i < ImageKeys.Count; i++)
                        {
                            if (sw.Elapsed.TotalSeconds > 10) break;
                            var image = this.ImageKeys[i];
                            var lastOffset = Rectangle.Empty;
                            var rf = af.ProcessImage(screen, image.Item2);
                            if (rf.Length == 0)
                            {
                                continue;
                            }
                            foundKeys.AddRange(rf.Select(x => new Tuple<ConsoleKey, Rectangle>(image.Item1, x.Rectangle)).ToArray());
                        }
                        screen.Dispose();
                        sw.Stop();
                        if (!new int[] { 5, 6, 7, 11 }.Contains(foundKeys.Count))
                        {
                            continue;
                        }
                        var keys = foundKeys.OrderBy(x => x.Item2.X).Select(x => x.Item1).ToArray();
                        Console.WriteLine($"Keys Found: {string.Join(", ", keys.Select(x => x.ToString()).ToArray())}");
                        Console.WriteLine($"Sending...");
                        foreach(var key in keys.Select(x =>
                        {
                            switch (x)
                            {
                                case ConsoleKey.UpArrow:
                                    return MemorySharpNative.Keys.Up;
                                case ConsoleKey.DownArrow:
                                    return MemorySharpNative.Keys.Down;
                                case ConsoleKey.LeftArrow:
                                    return MemorySharpNative.Keys.Left;
                                case ConsoleKey.RightArrow:
                                    return MemorySharpNative.Keys.Right;
                                case ConsoleKey.X:
                                    return MemorySharpNative.Keys.X;
                                case ConsoleKey.Z:
                                    return MemorySharpNative.Keys.Y;
                                case ConsoleKey.C:
                                    return MemorySharpNative.Keys.C;
                                default:
                                    return default;
                            }
                        }).Where(x => x != default).ToArray())
                        {
                            try
                            {
                                AppUtils.SetForegroundWindow(X2WindowHandle);
                                this.client.Windows.MainWindow.Keyboard.PressRelease(key);
                            } catch (Exception ex) { Console.WriteLine(ex.ToString()); }
                            await Task.Delay(150);
                        }
                        Console.Write("Press any key to continue...");
                        Console.ReadKey();
                    }
                }
                    await Task.Delay(1000);
            }
        }
        private Process getX2Process()
        {
            return Process.GetProcessesByName("x2").FirstOrDefault();
        }
    }
    internal static class AppUtils
    {
        internal static int FloatDiv(this int src, float div) => src / (int)(100.0f / div);
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool SendMessage(IntPtr hWnd, int wMsg, uint wParam, uint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        internal static extern int GetWindowRect(IntPtr hwnd, out Rectangle rect);
}
