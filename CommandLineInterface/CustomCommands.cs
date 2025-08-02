using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandLineInterface
{
    class CustomCommands
    {
        public static Action<string, Color> Append;

        public static Dictionary<string, Action<string>> Commands = new Dictionary<string, Action<string>>
        {
            { "about", About },
            { "abou2t", About2 },
        };


        public static void About(string arguments)
        {
            Append(" About command 1\n", Color.LawnGreen);
            Append($" {arguments}\n", Color.LawnGreen);
        }

        public static void About2(string arguments)
        {
            Append(" About command 2\n", Color.LawnGreen);
        }
    }
}
