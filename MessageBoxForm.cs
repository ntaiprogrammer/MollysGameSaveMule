using System;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/** 
 * ntaiprogrammer 26/01/2022
 */

namespace MollysGameSaveMule
{
    public partial class messageBoxForm : Form
    {
        //Import custom font.
        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbfont, uint cbfont
            , IntPtr pdv, [In] ref uint pcFonts);

        FontFamily ff;
        Font font;

        //Variables for window drag.
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        public messageBoxForm()
        {
            InitializeComponent();
        }

        //Get font from resources.
        private void LoadFont()
        {
            byte[] fontArray = MollysGameSaveMule.Properties.Resources.Heavitas;
            int dataLength = MollysGameSaveMule.Properties.Resources.Heavitas.Length;

            IntPtr ptrData = Marshal.AllocCoTaskMem(dataLength);

            Marshal.Copy(fontArray, 0, ptrData, dataLength);

            uint cFonts = 0;

            AddFontMemResourceEx(ptrData, (uint)fontArray.Length, IntPtr.Zero, ref cFonts);

            PrivateFontCollection pfc = new PrivateFontCollection();

            pfc.AddMemoryFont(ptrData, dataLength);

            Marshal.FreeCoTaskMem(ptrData);

            ff = pfc.Families[0];
            font = new Font(ff, 15f, FontStyle.Regular);
        }

        //Apply font to control item.
        private void AllocateFont(Font f, Control c, float size)
        {
            FontStyle fontStyle = FontStyle.Regular;

            c.Font = new Font(ff, size, fontStyle);
        }

        //Creates dynamic custom message box.
        public DialogResult ShowCustomDialog(string title, string text, bool yesNo)
        {
            if (yesNo == true)
            {
                btn_Yes.Visible = true;
                btn_No.Visible = true;
            }
            else
            {
                btn_OK.Visible = true;
            }

            label_TitleBar.Text = title;
            label_Message.Text = text;

            return this.ShowDialog();
        }

        //Click and drag to move the window around.
        //Most form elements plugged into this event.
        private void window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        //Load font from embedded resource and allocate font to all controls/elements.
        private void messageBoxForm_Load(object sender, EventArgs e)
        {
            LoadFont();
            AllocateFont(font, label_TitleBar, 18);
            AllocateFont(font, label_Message, 9);
            AllocateFont(font, btn_Yes, 12);
            AllocateFont(font, btn_No, 12);
            AllocateFont(font, btn_OK, 12);
        }
    }
}
