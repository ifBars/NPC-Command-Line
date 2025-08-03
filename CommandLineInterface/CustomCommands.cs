using CommandLineInterface.Properties;
using System.Diagnostics;

namespace CommandLineInterface
{
    class CustomCommands
    {
        public static Action<string, Color> Append;

        public static Dictionary<string, Action<string>> Commands = new Dictionary<string, Action<string>>
        {
            { "about", About },
            { "version", Version },
            { "code", Code },
            { "github", Code },
            { "alias", CreateAlias },
            { "createalias", CreateAlias },
        };

        public static void About(string arguments)
        {
            Append(" Welcome to ", Color.White);
            Append("NPC Terminal", Color.MediumSpringGreen);
            Append($" v.{Settings.Default.Version}!\n", Color.White);
            Append(" NPC Terminal is a clone of Windows Terminal but better. With Quality of Life features, better design, and more.\n Supports adding custom commands. Made by youtube.com/@CsharpProgramming\n", Color.White);
        }

        public static void Version(string arguments)
        {
            Append(" NPC Terminal", Color.MediumSpringGreen);
            Append($" v.{Settings.Default.Version}!\n", Color.White);
        }

        public static void Code(string arguments)
        {
            Append(" NPC Terminal", Color.MediumSpringGreen);
            Append(" code available at github.com/CsharpProgramming/NPC-Command-Line\n", Color.White);
        }

        public static void CreateAlias(string arguments)
        {
            try
            {
                var notepad = new Process();
                notepad.StartInfo = new ProcessStartInfo("notepad.exe", Path.Combine(Application.StartupPath, "aliases.txt"));
                notepad.EnableRaisingEvents = true;
                notepad.Exited += (s, e) => { Form1.Instance.Invoke(() => { Form1.Instance.LoadAliasesFromFile(); }); };
                notepad.Start();
            }

            catch (Exception e)
            {
                Append(" Error opening the alias file. Please report it:\n", Color.Yellow);
                Append($" Error: {e}", Color.Yellow);
            }
        }
    }
}