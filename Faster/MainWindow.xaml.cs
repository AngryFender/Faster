using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Forms.ContextMenu;

namespace Faster
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private Queue<double> _wpms = new Queue<double>();
        private readonly Mutex _mutex;
        public MainWindow()
        {
            bool isNewMap = true;
            _mutex = new Mutex(true, "Faster", out isNewMap);


            if (!isNewMap)
            {
                System.Windows.MessageBox.Show("Faster already running", "Multiple Instances", MessageBoxButton.OK, MessageBoxImage.Error);
                this.CloseApplication();
            }
            else
            {
                InitializeComponent();

                this._notifyIcon = new NotifyIcon
                {
                    BalloonTipText = "Faster is minimized to tray",
                    BalloonTipTitle = "Faster",
                    Text = "Faster",
                    Icon = Properties.Resources.Faster,
                    Visible = true
                };

                ContextMenu trayMenu = new ContextMenu();
                trayMenu.MenuItems.Add("Exit", (s, e) => CloseApplication());
                _notifyIcon.ContextMenu = trayMenu;
                this.Loaded += (sender, e) =>
                {
                    WindowInteropHelper helper = new WindowInteropHelper(this);
                    IntPtr hwnd = helper.Handle;
                    WinMonitor.GetInstance().WPMChanged += WPMChangedHandler;
                    WinMonitor.GetInstance().HideWPM += HideWPMHandler;
                    WinMonitor.GetInstance().BackSpaceKeyPressed += DeleteKeyPressedHandler;
                    WinMonitor.GetInstance().SetCurrentHWnd(hwnd);
                    WinMonitor.GetInstance().SetUIDispatcher(this.Dispatcher);
                };
                TxtWPM.Visibility = Visibility.Collapsed;
                ImgLow.Visibility = Visibility.Collapsed;
                ImgMid.Visibility = Visibility.Collapsed;
                ImgFire.Visibility = Visibility.Collapsed;
                ImgDelete.Visibility = Visibility.Collapsed;
            }
        }

        private void DeleteKeyPressedHandler(object sender, bool e)
        {
            if (e)
            {
                TxtWPM.Visibility = Visibility.Collapsed;
                ImgLow.Visibility = Visibility.Collapsed;
                ImgMid.Visibility = Visibility.Collapsed;
                ImgFire.Visibility = Visibility.Collapsed;
                ImgDelete.Visibility = Visibility.Visible;
            }
            else
            {
                ImgDelete.Visibility = Visibility.Collapsed;
            }
        }

        private void HideWPMHandler(object sender, EventArgs e)
        {
            TxtWPM.Visibility = Visibility.Collapsed;
            ImgLow.Visibility = Visibility.Collapsed;
            ImgMid.Visibility = Visibility.Collapsed;
            ImgFire.Visibility = Visibility.Collapsed;
            ImgDelete.Visibility = Visibility.Collapsed;
        }

        private void CloseApplication()
        {
            Application.Current.Shutdown();
        }

        private void WPMChangedHandler(object sender, WPMArgs e)
        {
            TxtWPM.Visibility = Visibility.Visible;

            _wpms.Enqueue(e.WPM);
            if (_wpms.Count > 5)
            {
                _wpms.Dequeue();
            }

            double averageWpm = 0;
            foreach (var wpm in _wpms)
            {
                averageWpm += wpm;
            }
            averageWpm = averageWpm / _wpms.Count;
            TxtWPM.Text = averageWpm.ToString();

            if(averageWpm >= 80 && averageWpm < 150)
            {
                ImgMid.Visibility = Visibility.Visible;
                ImgLow.Visibility = Visibility.Collapsed;
                ImgFire.Visibility = Visibility.Collapsed;
                ImgDelete.Visibility = Visibility.Collapsed;
            }
            else if (averageWpm >= 150)
            {
                ImgFire.Visibility = Visibility.Visible;
                ImgLow.Visibility = Visibility.Collapsed;
                ImgMid.Visibility = Visibility.Collapsed;
                ImgDelete.Visibility = Visibility.Collapsed;
            }
            else
            {
                ImgLow.Visibility = Visibility.Visible;
                ImgMid.Visibility = Visibility.Collapsed;
                ImgFire.Visibility = Visibility.Collapsed;
                ImgDelete.Visibility = Visibility.Collapsed;
            }
        }
    }
}
