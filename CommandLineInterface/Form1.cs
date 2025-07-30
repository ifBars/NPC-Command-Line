using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CommandLineInterface
{
    public partial class Form1 : Form
    {
        string prompt = " NPC > ";
        int promptStartIndex = 0;
        enum DwmWindowAttribute : uint
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            DWMWA_MICA_EFFECT = 38,
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute attr, ref int attrValue, int attrSize);

        public Form1()
        {
            InitializeComponent();

            AppendPrompt();
            ThemeAllControls();
        }

        private void ThemeAllControls(Control parent = null)
        {
            parent = parent ?? this;
            Action<Control> Theme = control => {
                int trueValue = 0x01;
                SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
                DwmSetWindowAttribute(control.Handle, DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueValue, Marshal.SizeOf(typeof(int)));
                DwmSetWindowAttribute(control.Handle, DwmWindowAttribute.DWMWA_MICA_EFFECT, ref trueValue, Marshal.SizeOf(typeof(int)));
            };
            if (parent == this) Theme(this);
            foreach (Control control in parent.Controls)
            {
                Theme(control);
                if (control.Controls.Count != 0)
                    ThemeAllControls(control);
            }
        }

        private void AppendPrompt()
        {
            AppendColoredText(prompt, Color.MediumSpringGreen);
            promptStartIndex = richTextBox1.TextLength;
            richTextBox1.SelectionColor = richTextBox1.ForeColor;
        }

        private void ExecuteCommand(string cmd)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + cmd)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = psi };
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(output))
                    AppendColoredText(output, Color.LightGray);
                if (!string.IsNullOrWhiteSpace(error))
                    AppendColoredText(error, Color.Red);
            }

            catch (Exception ex)
            {
                AppendColoredText("Error: " + ex.Message + Environment.NewLine, Color.Red);
            }

            AppendPrompt();
        }

        private void AppendColoredText(string text, Color color)
        {
            richTextBox1.SelectionStart = richTextBox1.TextLength;
            richTextBox1.SelectionColor = color;
            richTextBox1.AppendText(text);
            richTextBox1.SelectionColor = richTextBox1.ForeColor;
        }

        private string GetCurrentCommand()
        {
            if (richTextBox1.TextLength <= promptStartIndex) return "";
            return richTextBox1.Text.Substring(promptStartIndex).Trim();
        }

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back && richTextBox1.SelectionStart <= promptStartIndex)
            {
                e.SuppressKeyPress = true;
                return;
            }

            if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.Up || e.KeyCode == Keys.Home) &&
                richTextBox1.SelectionStart <= promptStartIndex)
            {
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string cmd = GetCurrentCommand();
                AppendColoredText(Environment.NewLine, Color.White);

                if (cmd.ToLower() == "clear")
                {
                    richTextBox1.Clear();
                    AppendPrompt();
                    return;
                }

                ExecuteCommand(cmd);
            }
        }
    }
}
