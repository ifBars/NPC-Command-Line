namespace CommandLineInterface.Core
{
    public interface ICommand
    {
        string Name { get; }
        string Description { get; }
        Task<bool> ExecuteAsync(string arguments, TerminalContext context);
    }
}


