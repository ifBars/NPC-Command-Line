using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLineInterface.Properties;

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
    }
}
