using System;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;

namespace ThinkingHome.NooLite.Console
{
    class Program
    {
        private static CommandOption<string> optionPort;
        private static CommandOption<byte> optionChannel;
        private static CommandOption optionMode;

        static void Invoke(Action<MTRFXXAdapter, byte> action, Action<MTRFXXAdapter, byte> actionF)
        {
            using (var adapter = new MTRFXXAdapter(optionPort.ParsedValue))
            {
                adapter.Open();
                adapter.ExitServiceMode();


                if (optionMode.HasValue())
                {
                    actionF(adapter, optionChannel.ParsedValue);
                }
                else
                {
                    action(adapter, optionChannel.ParsedValue);
                }
            }
        }

        static void Main(string[] args)
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;

            var app = new CommandLineApplication();
            app.Name = "noolite";
            app.Description = $"nooLite command line interface - v{ver.Major}.{ver.Minor}.{ver.Revision}";
            app.HelpOption("-?|-h|--help");
            app.ExtendedHelpText = "\nSee the details on https://github.com/thinking-home/noolite#readme.";

            optionPort = app.Option<string>("-p|--port", "Serial port name which nooLite adapter connected to.", CommandOptionType.SingleValue);
            optionChannel = app.Option<byte>("-c|--channel", "Target channel.", CommandOptionType.SingleValue);
            optionMode = app.Option("-f", "Set the nooLite-F mode.", CommandOptionType.NoValue);

            app.Command("bind", cmd =>
            {
                cmd.Description = "123";
                cmd.OnExecute(() => Invoke((a, c) => a.Bind(c), (a, c) => a.BindF(c)));
            });

            app.Command("unbind", cmd =>
            {
                cmd.Description = "123";
                cmd.OnExecute(() => Invoke((a, c) => a.Unbind(c), (a, c) => a.UnbindF(c)));
            });

            app.Command("on", cmd =>
            {
                cmd.Description = "123";
                cmd.OnExecute(() => Invoke((a, c) => a.On(c), (a, c) => a.OnF(c)));
            });

            app.Command("off", cmd =>
            {
                cmd.Description = "123";
                cmd.OnExecute(() => Invoke((a, c) => a.Off(c), (a, c) => a.OffF(c)));
            });

            app.Command("switch", cmd =>
            {
                cmd.Description = "123";
                cmd.OnExecute(() => Invoke((a, c) => a.Switch(c), (a, c) => a.SwitchF(c)));
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