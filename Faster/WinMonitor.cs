using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using System.Windows.Threading;

namespace Faster
{
    public class WinMonitor: IDisposable
    {
        private static WinMonitor instance;
        private KeyboardProc _keyboardProc;
        private IntPtr _keyboardEventHook;
        private static System.Timers.Timer _timer;
        private static int _countKeys;
        private readonly int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static Dispatcher _uiDispatcher;

        private delegate IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern bool GetCaretPos(out Position pos);

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook
            , KeyboardProc lpfn
            , IntPtr hmod
            , uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk
            , int nCode
            , IntPtr wParam
            , IntPtr lParam);

        private WinMonitor() 
        {
            _keyboardProc = new KeyboardProc(HandleKeyboardEvent);

             IntPtr hInstance = LoadLibrary("User32");
            _keyboardEventHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hInstance,  0);
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += TimerElapsedHandler;
            _timer.AutoReset = true;
        }

        public event EventHandler<WPMArgs> WPMChanged;

        public void RaiseWPMChanged()
        {
            if(null == _uiDispatcher )
            {
                return;
            }

            _uiDispatcher.Invoke(() =>
            {
                WPMChanged?.Invoke(this, new WPMArgs(_countKeys));
            });
        }

        private void TimerElapsedHandler(object sender, ElapsedEventArgs e)
        {
            if (null == _keyboardEventHook)
            {
                return;
            }

            _timer.Stop();
            RaiseWPMChanged();
            Interlocked.Exchange(ref _countKeys, 0);
        }

        private IntPtr HandleKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if ( nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                if (!_timer.Enabled)
                {
                    _timer.Start();
                }
                Interlocked.Increment(ref _countKeys);
            }
            return CallNextHookEx(_keyboardEventHook, nCode, wParam, lParam);
        }

        public static WinMonitor GetInstance()
        {
            if (instance == null)
            {
                instance = new WinMonitor();
            }
            return instance;
        }

        public static void SetUIDispatcher(Dispatcher ui)
        {
            _uiDispatcher = ui;
        }

        public (long,long) GetCaretPosition()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (null != hWnd )
            {
                Position pos;
                GetCaretPos(out pos);
                return (pos.x, pos.y);
            }
            return (0, 0);
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(_keyboardEventHook);
        }
    }

    public struct Position
    {
        public long x;
        public long y;
    }

    public class WPMArgs: EventArgs
    {
        public int WPM { get; }

        public WPMArgs(int wpm)
        {
            WPM = wpm;
        }
    }
}
