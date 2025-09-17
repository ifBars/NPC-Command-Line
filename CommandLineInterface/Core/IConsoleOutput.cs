using System.Drawing;

namespace CommandLineInterface.Core
{
    public interface IConsoleOutput
    {
        void Write(string text);
        void Write(string text, Color color);
        void WriteLine(string text);
        void WriteLine(string text, Color color);
    }
}


