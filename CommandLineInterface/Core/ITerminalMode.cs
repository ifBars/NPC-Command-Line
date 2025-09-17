namespace CommandLineInterface.Core
{
    public interface ITerminalMode
    {
        string Name { get; }
        bool IsActive { get; }
        Task EnterAsync(TerminalContext context);
        Task ExitAsync(TerminalContext context);
        Task<bool> HandleAsync(string input, TerminalContext context);
    }
}


