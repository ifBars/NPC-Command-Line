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
        List<string> CommandHistory = new();
        int HistoryIndex = 0;
        CommandLineInterface.Core.CommandRouter Router = new();
        CommandLineInterface.Core.TerminalContext? TerminalContext;

        Dictionary<string, string> CommandAliases = new();
        string AliasFilePath = Path.Combine(Application.StartupPath, "aliases.txt");
        public static Form1 Instance;
        Color RegularTextColor = Color.White;

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
            
            try
            {
                var codexService = new Services.CodexService(new Uri("http://localhost:11434/"), "gpt-oss:20b");
                TerminalContext = new CommandLineInterface.Core.TerminalContext
                {
                    Append = AppendSafe,
                    GetWorkspaceRoot = GetWorkspaceRoot,
                    CodexService = codexService,
                    Form = this
                };
                
                // Register all modern commands
                CommandLineInterface.Core.CommandFactory.RegisterAllCommands(Router);
                
                // Register codex command
                Router.Register(new CommandLineInterface.Commands.CodexTerminalCommand());
            }
            catch
            {
                // Silent; command will report if uninitialized
            }

            ThemeUI();
        }

        private void ThemeUI()
        {
            if (Settings.Default.DarkMode == true)
            {
                this.BackColor = Color.FromArgb(45, 45, 48);
                ThemeAllControls();
                richTextBox1.BackColor = Color.FromArgb(30, 30, 30);
                RegularTextColor = Color.White;
                richTextBox1.ForeColor = Color.White;
                richTextBox1.Clear();
                AppendPrompt();
                return;
            }
            else
            {
                this.BackColor = SystemColors.Control;
                ResetControlsToLightTheme();
                richTextBox1.BackColor = Color.White;
                RegularTextColor = Color.Black;
                richTextBox1.ForeColor = Color.Black;
                richTextBox1.Clear();
                AppendPrompt();
                return;
            }
        }

        private void ResetControlsToLightTheme(Control parent = null)
        {
            parent ??= this;

            Action<Control> ResetTheme = control =>
            {
                SetWindowTheme(control.Handle, "", "");

                int falseValue = 0x00;
                DwmSetWindowAttribute(control.Handle, DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE, ref falseValue, Marshal.SizeOf(typeof(int)));
            };

            if (parent == this) ResetTheme(this);

            foreach (Control control in parent.Controls)
            {
                if (control != richTextBox1)
                {
                    ResetTheme(control);
                }

                if (control.Controls.Count != 0)
                {
                    ResetControlsToLightTheme(control);
                }
            }
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

                if (TerminalContext != null)
                {
                    var routed = await Router.TryRouteAsync(Command, TerminalContext);
                    if (routed)
                    {
                        AppendPrompt();
                        return;
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

                if (Parts.Length > 0 && Parts[0].ToLower() == "theme")
                {
                    Settings.Default.DarkMode = !Settings.Default.DarkMode;
                    Settings.Default.Save();
                    ThemeUI();
                    return;
                }
            }

            // Old CustomCommands system removed - all commands now go through CommandRouter

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
                    AppendColoredText(" custom commands:\n", RegularTextColor);
                    
                    // Display commands by category
                    var categories = CommandLineInterface.Core.CommandFactory.GetCommandsByCategory();
                    foreach (var category in categories)
                    {
                        AppendColoredText($"\n {category.Key.ToUpper()}:\n", Color.Cyan);
                        foreach (var cmd in category.Value.Take(5)) // Show first 5 commands per category
                        {
                            var aliases = cmd is CommandLineInterface.Core.BaseCommand baseCmd && baseCmd.Aliases.Length > 0 
                                ? $" ({string.Join(", ", baseCmd.Aliases)})" 
                                : "";
                            AppendColoredText($" {cmd.Name.ToUpper()}{aliases.ToLower()}  {cmd.Description}\n", RegularTextColor);
                        }
                    }
                    
                    AppendColoredText("\n BUILT-IN:\n", Color.Cyan);
                    AppendColoredText(" MODE           Toggle CMD/PowerShell mode.\n", RegularTextColor);
                    AppendColoredText(" THEME          Toggle dark/light theme.\n", RegularTextColor);
                    AppendColoredText(" CD             Change directory.\n", RegularTextColor);
                    AppendColoredText(" CLEAR, C       Clear terminal.\n", RegularTextColor);
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

        private void AppendSafe(string text, Color color)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AppendColoredText(text, color)));
            }
            else
            {
                AppendColoredText(text, color);
            }
        }

        private string GetCurrentCommand()
        {
            if (richTextBox1.TextLength <= PromptStartIndex)
            {
                return "";
            }

            return richTextBox1.Text.Substring(PromptStartIndex).Trim();
        }

        private void SetCurrentCommand(string text)
        {
            richTextBox1.SelectionStart = PromptStartIndex;
            richTextBox1.SelectionLength = richTextBox1.TextLength - PromptStartIndex;
            richTextBox1.SelectedText = text;
            richTextBox1.SelectionStart = richTextBox1.TextLength;
        }

        private string GetWorkspaceRoot()
        {
            try
            {
                var dir = new DirectoryInfo(Application.StartupPath);
                for (var current = dir; current != null; current = current.Parent)
                {
                    // Prefer solution root
                    if (current.GetFiles("*.sln").Any()) return current.FullName;
                    // Or git repo root
                    if (current.GetDirectories(".git").Any()) return current.FullName;
                    // Or the project root containing the csproj
                    if (current.GetFiles("*.csproj").Any()) return current.FullName;
                }
                return dir.FullName;
            }
            catch
            {
                return Application.StartupPath;
            }
        }

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;
                if (CommandHistory.Count == 0) return;
                if (HistoryIndex > 0) HistoryIndex--;
                SetCurrentCommand(CommandHistory[HistoryIndex]);
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                if (CommandHistory.Count == 0) return;
                if (HistoryIndex < CommandHistory.Count - 1)
                {
                    HistoryIndex++;
                    SetCurrentCommand(CommandHistory[HistoryIndex]);
                }
                else
                {
                    HistoryIndex = CommandHistory.Count;
                    SetCurrentCommand("");
                }
                return;
            }

            if (e.KeyCode == Keys.Back && richTextBox1.SelectionStart <= PromptStartIndex)
            {
                e.SuppressKeyPress = true;
                return;
            }

            if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.Home) && richTextBox1.SelectionStart <= PromptStartIndex)
            {
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string Command = GetCurrentCommand();
                AppendColoredText(Environment.NewLine, RegularTextColor);

                if (!string.IsNullOrWhiteSpace(Command))
                {
                    CommandHistory.Add(Command);
                    HistoryIndex = CommandHistory.Count;
                }

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
                File.WriteAllText(AliasFilePath, "gcm\ngit commit -m\n"); //Create the text file with an example command
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
