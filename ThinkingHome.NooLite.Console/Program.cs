using System;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;

namespace ThinkingHome.NooLite.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;

            var app = new CommandLineApplication();
            app.Name = "noolite";
            app.Description = $"nooLite command line interface - v{ver.Major}.{ver.Minor}.{ver.Revision}";
            app.HelpOption("-?|-h|--help");
            app.ExtendedHelpText = "\nSee the details on https://github.com/thinking-home/noolite#readme.";

            var port = app.Option<string>("-p|--port", "Serial port name which nooLite adapter connected to", CommandOptionType.SingleValue);
            var chanel = app.Option<int>("-c|--channel", "Target channel", CommandOptionType.SingleValue);

            app.Command("on", cmd => { cmd.Description = "123"; });
            app.Command("off", cmd => { cmd.Description = "456"; });

            app.OnExecute(() =>
            {
                System.Console.WriteLine($"executed: {port.ParsedValue}, channel: {chanel.ParsedValue}");
            });

            try
            {
                app.Execute(args);
            }
            catch (CommandParsingException e)
            {
                System.Console.Error.WriteLine(e.Message);
                app.ShowHelp();
                Environment.ExitCode = 1;
            }
        }
    }
}