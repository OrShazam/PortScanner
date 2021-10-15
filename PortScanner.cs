using System;
using System.Net.NetworkInformation;
using System.Collections;
using System.Net;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace PortScanner
{
    // the HotKeyManager is utterly stolen from StackOverflow
    public static class HotKeyManager
    {
        public static event EventHandler<HotKeyEventArgs> HotKeyPressed;

        public static int RegisterHotKey(Keys key, KeyModifiers modifiers)
        {
            _windowReadyEvent.WaitOne();
            _wnd.Invoke(new RegisterHotKeyDelegate(RegisterHotKeyInternal), _hwnd, Interlocked.Increment(ref _id), (uint)modifiers, (uint)key);
            return Interlocked.Increment(ref _id);
        }

        public static void UnregisterHotKey(int id)
        {
            _wnd.Invoke(new UnRegisterHotKeyDelegate(UnRegisterHotKeyInternal), _hwnd, id);
        }

        private delegate void RegisterHotKeyDelegate(IntPtr hwnd, int id, uint modifiers, uint key);
        private delegate void UnRegisterHotKeyDelegate(IntPtr hwnd, int id);

        private static void RegisterHotKeyInternal(IntPtr hwnd, int id, uint modifiers, uint key)
        {
            RegisterHotKey(hWnd: hwnd, id: id, fsModifiers: modifiers, vk: key);
        }

        private static void UnRegisterHotKeyInternal(IntPtr hwnd, int id)
        {
            UnregisterHotKey(_hwnd, id);
        }

        private static void OnHotKeyPressed(HotKeyEventArgs e)
        {
            HotKeyPressed?.Invoke(null, e);
        }

        private static volatile MessageWindow _wnd;
        private static volatile IntPtr _hwnd;
        private static ManualResetEvent _windowReadyEvent = new ManualResetEvent(false);

        static HotKeyManager()
        {
            new Thread(delegate ()
            {
                Application.Run(new MessageWindow());
            })
            {
                Name = "MessageLoopThread",
                IsBackground = true
            }.Start();
        }

        private class MessageWindow : Form
        {
            public MessageWindow()
            {
                _wnd = this;
                _hwnd = Handle;
                _windowReadyEvent.Set();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    var e = new HotKeyEventArgs(hotKeyParam: m.LParam);
                    OnHotKeyPressed(e);
                }

                base.WndProc(m: ref m);
            }

            protected override void SetVisibleCore(bool value)
            {
                // Ensure the window never becomes visible
                base.SetVisibleCore(false);
            }

            private const int WM_HOTKEY = 0x312;
        }

        [DllImport("user32", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private static int _id = 0;
    }
    [Flags]
    public enum KeyModifiers
    {
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8,
        NoRepeat = 0x4000
    }
    public class HotKeyEventArgs : EventArgs
    {
        public readonly Keys Key;
        public readonly KeyModifiers Modifiers;

        public HotKeyEventArgs(Keys key, KeyModifiers modifiers)
        {
            this.Key = key;
            this.Modifiers = modifiers;
        }

        public HotKeyEventArgs(IntPtr hotKeyParam)
        {
            uint param = (uint)hotKeyParam.ToInt64();
            Key = (Keys)((param & 0xffff0000) >> 16);
            Modifiers = (KeyModifiers)(param & 0x0000ffff);
        }
    }
    class Program
    {
        enum ConnectionType
        {
            TCP = 0,UDP
        }
        class PortInfo
        {
            public PortInfo(string localAddress,ConnectionType type)
            {
                this.localAddress = localAddress;this.type = type;
            }
            public string localAddress;
            public ConnectionType type;
        }
        static void ListActivePorts(ref SortedDictionary<int,PortInfo> portData)
        {
            IPGlobalProperties IPProps = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArr = IPProps.GetActiveTcpConnections();
            IPEndPoint[] udpConnInfoArr = IPProps.GetActiveUdpListeners();
            IEnumerator myEnum = tcpConnInfoArr.GetEnumerator();
            while (myEnum.MoveNext())
            {
                TcpConnectionInformation TCPInfo = (TcpConnectionInformation)myEnum.Current;
                int port = TCPInfo.LocalEndPoint.Port;
                string localAddress = TCPInfo.LocalEndPoint.ToString();
                if (portData.ContainsKey(port))
                    continue;
                portData.Add(port, new PortInfo(localAddress, ConnectionType.TCP));
            }
            myEnum = udpConnInfoArr.GetEnumerator();
            while (myEnum.MoveNext())
            {
                IPEndPoint ep = (IPEndPoint)myEnum.Current;
                int port = ep.Port;
                if (portData.ContainsKey(port))
                    continue;
                string localAddress = ep.ToString();
                portData.Add(port, new PortInfo(localAddress, ConnectionType.UDP));
            }
        }

        static void Exit()
        {
            HotKeyManager.UnregisterHotKey(0);
            HotKeyManager.UnregisterHotKey(1);
        }
        static void ChangeSpeed(object sender, HotKeyEventArgs e)
        {
            if (e.Key == Keys.Up)
            {
                sleepTime -= 50;
            }
            else if (e.Key == Keys.Down)
            {
                sleepTime += 50;
            }

        }
        private static int sleepTime = 1000;
        static void Main(string[] args)
        { 
            SortedDictionary<int, PortInfo> portData = new SortedDictionary<int, PortInfo>();
            ListActivePorts(ref portData);

            HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(ChangeSpeed);
            HotKeyManager.RegisterHotKey(Keys.Up, KeyModifiers.Shift);
            HotKeyManager.RegisterHotKey(Keys.Down, KeyModifiers.Shift);
            Console.WriteLine("\nPress [SHIFT] + [UP] to move faster or [SHIFT] + [DOWN] to move slower");
            Console.WriteLine("Listing data about 1024 first ports...");
            Console.Write("\t|PORT|\t|Local Address|\t|Type|");
            for (int i = 1; i <= 1024; i++)
            {
                    if (portData.ContainsKey(i))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write($"\t{i}\t{portData[i].localAddress}\t{portData[i].type.ToString()}");
                        Console.ResetColor();
                    }
                    else
                        Console.WriteLine($"\t{i}");
                if (sleepTime > 0)
                    Thread.Sleep(sleepTime);
            }
            Console.Write("To List data about the rest of the ports? (y/n) ");
            if (Console.ReadLine().ToLower()[0] == 'y')
            {
                for (int i = 1025; i <= 65535; i++)
                {
                    if (portData.ContainsKey(i))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write($"\t{i}\t{portData[i].localAddress}\t{portData[i].type.ToString()}");
                        Console.ResetColor();
                    }
                    else
                        Console.WriteLine($"\t{i}");
                    if (sleepTime > 0)
                        Thread.Sleep(sleepTime);

                }
            }
            Console.ReadKey();
        }
    }
}
