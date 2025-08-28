using NPC_Terminal.Properties;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CommandLineInterface
{
    public partial class Form1 : Form
    {
        int PromptStartIndex = 0;
        string CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string CurrentMode = "cmd"; 

        Dictionary<string, string> CommandAliases = new();
        string AliasFilePath = Path.Combine(Application.StartupPath, "aliases.txt");
        public static Form1 Instance;

        //Dark mode stuff
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

            Instance = this;
            LoadAliasesFromFile();
            CustomCommands.Append = AppendColoredText; //Pass the text appending function to the custom command system.

            AppendPrompt();
            ThemeAllControls();
        }

        private void ThemeAllControls(Control parent = null) //Dark mode from: stackoverflow.com/questions/72988434/how-to-make-winform-use-the-system-dark-mode-theme
        {
            parent ??= this;
            Action<Control> Theme = control =>
            {
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
            AppendColoredText(" NPC ", Color.MediumSpringGreen);
            AppendColoredText("[" + CurrentDirectory + "]", Color.Gray);
            AppendColoredText(" > ", Color.MediumSpringGreen);
            PromptStartIndex = richTextBox1.TextLength;
            richTextBox1.SelectionColor = richTextBox1.ForeColor;
        }

        private async void ExecuteCommand(string Command)
        {
            //Resources used:
            //powershellcommands.com/execute-powershell-script-from-c
            //stackoverflow.com/questions/1469764/run-command-prompt-commands
            //foxlearn.com/csharp/how-to-run-commands-in-command-prompt-using-csharp-8394.html
            //Other small stuff and around 20% of the code from Claude Sonnet 4.

            Stopwatch stopwatch = new();
            stopwatch.Start();

            string[] Parts = Command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            if (Parts.Length > 0)
            {
                if (Parts.Length > 0 && CommandAliases.TryGetValue(Parts[0], out var mapped))
                {
                    Command = mapped;
                    if (Parts.Length > 1)
                    {
                        Command += " " + Parts[1];
                    }
                }

                if (Parts.Length > 0 && Parts[0].ToLower() == "cd") //If change directory command runs update the directory displayed in the Prompt.
                {
                    string NewDirectory;
                    if (Parts.Length == 1)
                    {
                        NewDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    }

                    else
                    {
                        NewDirectory = Parts[1];
                    }

                    try
                    {
                        string Combined = Path.IsPathRooted(NewDirectory)
                            ? NewDirectory
                            : Path.Combine(CurrentDirectory, NewDirectory);

                        string FullPath = Path.GetFullPath(Combined);

                        if (Directory.Exists(FullPath))
                        {
                            CurrentDirectory = FullPath;
                        }

                        else
                        {
                            AppendColoredText(" The specified path can not be found.\n", Color.Yellow);
                        }
                    }

                    catch (Exception ex)
                    {
                        AppendColoredText(" Error: " + ex.Message + "\n", Color.Red);
                    }

                    AppendPrompt();
                    return;
                }

                if (Parts.Length > 0 && Parts[0].ToLower() == "mode") //Run switch mode command.
                {
                    if (CurrentMode == "cmd")
                    {
                        CurrentMode = "powershell";
                    }

                    else
                    {
                        CurrentMode = "cmd";
                    }

                    richTextBox1.Clear();
                    AppendColoredText(" NPC", Color.MediumSpringGreen);
                    AppendColoredText($" Switched to {CurrentMode.ToUpper()} mode.\n", Color.Gray);
                    AppendPrompt();
                    return;
                }
            }

            if (CustomCommands.Commands.TryGetValue(Command, out var action))
            {
                action.Invoke(CurrentDirectory);
                AppendPrompt();
                return;
            }

            try
            {
                string Executable;
                Executable = "cmd.exe";
                if (CurrentMode == "powershell")
                {
                    Executable = "powershell.exe";
                }

                string Arguments;
                Arguments = $"/c {Command}";
                if (CurrentMode == "powershell")
                {
                    Arguments = $"-Command \"{Command}\"";
                }

                var psi = new ProcessStartInfo(Executable, Arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = CurrentDirectory
                };

                var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        Invoke(() => AppendColoredText($" {e.Data}\n", Color.LightGray));
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        Invoke(() => AppendColoredText($" {e.Data}\n", Color.Red));
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(process.WaitForExit);

                if (Command.ToLower() == "help")
                {
                    AppendColoredText("\n NPC Terminal", Color.MediumSpringGreen);
                    AppendColoredText(" custom commands:\n", Color.White);
                    AppendColoredText(" ABOUT          Information About NPC Terminal and its features.\n", Color.White);
                    AppendColoredText(" CODE, GITHUB   Where can you find the code of the Terminal.\n", Color.White);
                    AppendColoredText(" VERSION        Current program version.\n", Color.White);
                    AppendColoredText(" MODE           Toggle CMD/PowerShell mode.\n", Color.White);
                }
            }

            catch (Exception ex)
            {
                AppendColoredText(" Error: " + ex.Message + "\n", Color.Red);
            }

            stopwatch.Stop();
            AppendColoredText($" NPC", Color.MediumSpringGreen);
            AppendColoredText($" Command completed in {stopwatch.ElapsedMilliseconds}ms\n", Color.Gray);
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
            if (richTextBox1.TextLength <= PromptStartIndex)
            {
                return "";
            }

            return richTextBox1.Text.Substring(PromptStartIndex).Trim();
        }

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back && richTextBox1.SelectionStart <= PromptStartIndex)
            {
                e.SuppressKeyPress = true;
                return;
            }

            if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.Up || e.KeyCode == Keys.Home) && richTextBox1.SelectionStart <= PromptStartIndex)
            {
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string Command = GetCurrentCommand();
                AppendColoredText(Environment.NewLine, Color.White);

                if (Command.ToLower() == "clear" || Command.ToLower() == "c")
                {
                    richTextBox1.Clear();
                    AppendPrompt();
                    return;
                }

                ExecuteCommand(Command);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Text = "NPC Terminal v." + Settings.Default.Version;
        }

        public void LoadAliasesFromFile()
        {
            CommandAliases.Clear();

            if (!File.Exists(AliasFilePath))
            {
                File.WriteAllText(AliasFilePath, "gcm\ngit commit -m"); //Create the text file with an example command
            }

            string[] Lines = File.ReadAllLines(AliasFilePath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();

            for (int i = 0; i < Lines.Length - 1; i += 2)
            {
                string Alias = Lines[i].Trim();
                string Command = Lines[i + 1].Trim();

                if (!CommandAliases.ContainsKey(Alias))
                {
                    CommandAliases.Add(Alias, Command);
                }
            }
        }
    }
}
