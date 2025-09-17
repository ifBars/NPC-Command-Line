using CommandLineInterface.Commands.System;
using CommandLineInterface.Commands.Utility;
using CommandLineInterface.Commands.Fun;
using CommandLineInterface.Commands.Network;
using CommandLineInterface.Commands.Configuration;

namespace CommandLineInterface.Core
{
    /// <summary>
    /// Factory class responsible for creating and registering all available commands
    /// </summary>
    public static class CommandFactory
    {
        /// <summary>
        /// Registers all available commands with the command router
        /// </summary>
        public static void RegisterAllCommands(CommandRouter router)
        {
            // System Commands
            RegisterCommand(router, new AboutCommand());
            RegisterCommand(router, new VersionCommand());
            RegisterCommand(router, new CodeCommand());
            RegisterCommand(router, new SystemInfoCommand());

            // Configuration Commands
            RegisterCommand(router, new AliasCommand());

            // Utility Commands
            RegisterCommand(router, new TimeCommand());
            RegisterCommand(router, new CalculatorCommand());
            RegisterCommand(router, new HashCommand());
            RegisterCommand(router, new EncodeCommand());
            RegisterCommand(router, new WeatherCommand());
            RegisterCommand(router, new QrCommand());
            RegisterCommand(router, new UuidCommand());
            RegisterCommand(router, new PasswordCommand());

            // Network Commands
            RegisterCommand(router, new IpCommand());
            RegisterCommand(router, new NetworkCommand());

            // Fun Commands
            RegisterCommand(router, new SusCommand());
            RegisterCommand(router, new RickRollCommand());
            RegisterCommand(router, new DogeCommand());
            RegisterCommand(router, new CowSayCommand());
            RegisterCommand(router, new AsciiCommand());
            RegisterCommand(router, new LennyCommand());
            RegisterCommand(router, new MatrixCommand());
            RegisterCommand(router, new NyanCommand());
            RegisterCommand(router, new SigmaCommand());
            RegisterCommand(router, new YeetCommand());
            RegisterCommand(router, new RizzCommand());
        }

        private static void RegisterCommand(CommandRouter router, ICommand command)
        {
            // Register the main command
            router.Register(command);
            
            // Register any aliases if the command is a BaseCommand with aliases
            if (command is BaseCommand baseCommand && baseCommand.Aliases.Length > 0)
            {
                foreach (var alias in baseCommand.Aliases)
                {
                    router.Register(new CommandAlias(alias, command));
                }
            }
        }

        /// <summary>
        /// Register a command alias by creating a wrapper
        /// </summary>
        private static void RegisterAlias(CommandRouter router, string aliasName, ICommand originalCommand)
        {
            router.Register(new CommandAlias(aliasName, originalCommand));
        }

        /// <summary>
        /// Gets all available command categories for help display
        /// </summary>
        public static Dictionary<string, List<ICommand>> GetCommandsByCategory()
        {
            var categories = new Dictionary<string, List<ICommand>>
            {
                ["System"] = new List<ICommand>
                {
                    new AboutCommand(),
                    new VersionCommand(),
                    new CodeCommand(),
                    new SystemInfoCommand()
                },
                ["Configuration"] = new List<ICommand>
                {
                    new AliasCommand()
                },
                ["Utility"] = new List<ICommand>
                {
                    new TimeCommand(),
                    new CalculatorCommand(),
                    new HashCommand(),
                    new EncodeCommand(),
                    new WeatherCommand(),
                    new QrCommand(),
                    new UuidCommand(),
                    new PasswordCommand()
                },
                ["Network"] = new List<ICommand>
                {
                    new IpCommand(),
                    new NetworkCommand()
                },
                ["Fun"] = new List<ICommand>
                {
                    new SusCommand(),
                    new RickRollCommand(),
                    new DogeCommand(),
                    new CowSayCommand(),
                    new AsciiCommand(),
                    new LennyCommand(),
                    new MatrixCommand(),
                    new NyanCommand(),
                    new SigmaCommand(),
                    new YeetCommand(),
                    new RizzCommand()
                }
            };

            return categories;
        }
    }

    /// <summary>
    /// Wrapper class to handle command aliases
    /// </summary>
    internal class CommandAlias : ICommand
    {
        private readonly ICommand _originalCommand;
        
        public CommandAlias(string aliasName, ICommand originalCommand)
        {
            Name = aliasName;
            _originalCommand = originalCommand;
        }

        public string Name { get; }
        public string Description => _originalCommand.Description;

        public Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            return _originalCommand.ExecuteAsync(arguments, context);
        }
    }
}
