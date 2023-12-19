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
        private readonly KeyboardProc _keyboardProc;
        private readonly IntPtr _keyboardEventHook;
        private static System.Timers.Timer _timer;
        private static System.Timers.Timer _visibilityTimer;
        private static int _countKeys;
        private readonly int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_DELETE = 0x08;
        private bool _isBackSpacePressed = false;
        private static Dispatcher _uiDispatcher;
        private static IntPtr _currentHWnd;
        private static WinRect _currentRect;

        private delegate IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool MoveWindow(IntPtr hWnd,int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd,out WinRect lpRect);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd,ref  Position lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct WinRect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
   
        [StructLayout(LayoutKind.Sequential)]
        private struct Position
        {
            public int x;
            public int y;

            public Position(int x, int y)
            {
                this.x = x; this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public WinRect rcCaret;
        }

        private WinMonitor() 
        {
            _keyboardProc = new KeyboardProc(HandleKeyboardEvent);

             IntPtr hInstance = LoadLibrary("User32");
            _keyboardEventHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hInstance,  0);

            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += TimerElapsedHandler;
            _timer.AutoReset = true;

            _visibilityTimer = new System.Timers.Timer(5000);
            _visibilityTimer.Elapsed += VisibilityTimerElapsedHandler;
            _visibilityTimer.AutoReset = true;
        }


        public event EventHandler HideWPM;

        public void RaiseHideWPM()
        {
            if(null == _uiDispatcher)
            {
                return;
            }
            _uiDispatcher.Invoke(()=>{
                HideWPM?.Invoke(this, EventArgs.Empty);
            });
        }

        private void VisibilityTimerElapsedHandler(object sender, ElapsedEventArgs e)
        {
            RaiseHideWPM();
            _visibilityTimer?.Stop();
        }

        public event EventHandler<WPMArgs> WPMChanged;

        public void RaiseWPMChanged()
        {
            if(null == _uiDispatcher )
            {
                return;
            }

            float wpm = ((float)_countKeys * 60.0f) / (5.0f);
            _uiDispatcher.Invoke(() =>
            {
                WPMChanged?.Invoke(this, new WPMArgs((double)wpm));
            });

            _visibilityTimer.Stop();
            _visibilityTimer.Start();
            
        }

        private void TimerElapsedHandler(object sender, ElapsedEventArgs e)
        {
            if (null == _keyboardEventHook)
            {
                return;
            }

            _timer?.Stop();
            RaiseWPMChanged();
            Interlocked.Exchange(ref _countKeys, 0);
        }

        public event EventHandler<bool> BackSpaceKeyPressed;
        public void RaiseBackKeyPressed(bool isPressed)
        {
            _visibilityTimer.Stop();
            _visibilityTimer.Start();
            BackSpaceKeyPressed?.Invoke(this, isPressed);
        }

        private IntPtr HandleKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if ( nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_DELETE)
                {
                    RaiseBackKeyPressed(true);
                    _isBackSpacePressed = true;
                }
                else
                {
                    RaiseBackKeyPressed(false);

                    if (!_timer.Enabled)
                    {
                        _timer.Start();
                    }
                    Interlocked.Increment(ref _countKeys);
                }
                GetCaretPosition();
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

        public void SetCurrentHWnd(IntPtr hWnd)
        {
            _currentHWnd = hWnd;
            GetWindowRect(hWnd, out _currentRect );
        }

        public void SetUIDispatcher(Dispatcher ui)
        {
            _uiDispatcher = ui;
        }

        public void GetCaretPosition()
        {
            IntPtr hWnd = GetForegroundWindow();

            if(hWnd == _currentHWnd)
            {
                return;
            }

            if (null != hWnd )
            {
                GUITHREADINFO guiInfo = new GUITHREADINFO();
                guiInfo.cbSize = Marshal.SizeOf(guiInfo);
                GetGUIThreadInfo(0, ref guiInfo);

                Position pos = new Position
                {
                    x = guiInfo.rcCaret.left,
                    y = guiInfo.rcCaret.top
                };
                if (0 != pos.x && 0 != pos.y)
                {
                    ClientToScreen(hWnd,ref pos);
                    MoveWindow(_currentHWnd,(int) pos.x+80
                               , (int)pos.y - 15
                               , _currentRect.right - _currentRect.left
                               , _currentRect.bottom - _currentRect.top
                               , true);
                }
                else
                { 
                    GetWindowRect(hWnd, out WinRect winRect );
                    MoveWindow(_currentHWnd, winRect.right-100
                               , winRect.bottom-50
                               , _currentRect.right - _currentRect.left
                               , _currentRect.bottom - _currentRect.top
                               , true);
                }
            }
            return;
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(_keyboardEventHook);
        }
    }

    public class WPMArgs: EventArgs
    {
        public double WPM { get; }

        public WPMArgs(double wpm)
        {
            WPM = wpm;
        }
    }
}
