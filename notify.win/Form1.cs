﻿using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace notify.win
{
    public partial class Form1 : Form
    {

        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_APPWINDOW = 0x40000;
                const int WS_EX_TOOLWINDOW = 0x80;
                CreateParams cp = base.CreateParams;
                cp.ExStyle &= (~WS_EX_APPWINDOW);
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        private int speed;
        private string msg;
        private int star;
        public Form1(int speed, string msg,int star)
        {
            this.speed = speed;
            this.msg = msg;
            this.star = Math.Min(star,5);

            InitializeComponent();
            this.AllowTransparency = true;
            this.TransparencyKey = Color.Black;
            this.BackColor = Color.Black;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.ShowInTaskbar = false;

            //username.BackColor = Color.FromArgb(255, 248, 153);
            //username.ForeColor = Color.LightGreen;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            PictureBox[] stars = new PictureBox[] { star1, star2, star3, star4, star5 };
            for (int i = 0; i < 5; i++)
            {
                stars[i].Image = global::notify.win.Properties.Resources.star2;
            }
            for (int i = 0; i < star; i++)
            {
                stars[i].Image = global::notify.win.Properties.Resources.star1;
            }
            Bitmap[] images = new Bitmap[] { Properties.Resources._0, Properties.Resources._1, Properties.Resources._2, Properties.Resources._3, Properties.Resources._4, Properties.Resources._5 };
            pictureBox1.Image = images[new Random().Next(0,6)];

            this.FormBorderStyle = FormBorderStyle.None;
            //将窗口置顶
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            int style = GetWindowLong(this.Handle, GWL_STYLE);
            SetWindowLong(this.Handle, GWL_STYLE, style & ~WS_SYSMENU);

            // 添加窗体阴影
            SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_FRAMECHANGED | SWP_NOSIZE | SWP_NOMOVE);


            RECT workAreaRect;
            SystemParametersInfo(SPI_GETWORKAREA, 0, out workAreaRect, 0);

            int screenWidth = workAreaRect.Right;
            int screenHeight = workAreaRect.Bottom;
            int width = this.Width;
            int height = this.Height;

            username.Text = this.msg;

            Task.Run(() =>
            {
                this.Left = screenWidth + width;
                this.Top = screenHeight - height;
                while (true)
                {
                    this.Left -= this.speed;
                    if (this.Left < -width)
                    {
                        Application.Exit();
                    }

                    System.Threading.Thread.Sleep(10);
                }

            });
        }

        private const int SPI_GETWORKAREA = 0x0030;

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(int uAction, int uParam, out RECT lpRect, int fuWinIni);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }


        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public const uint SWP_NOMOVE = 0x2;
        public const uint SWP_NOSIZE = 0x1;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        private const uint SWP_FRAMECHANGED = 0x20;
    }
}
