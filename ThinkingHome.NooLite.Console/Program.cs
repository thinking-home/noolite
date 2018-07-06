using System;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;

namespace ThinkingHome.NooLite.Console
{
    class Program
    {
        class CommonArgs
        {
            public CommandArgument<string> Port { get; set; }
            public CommandArgument<byte> Channel { get; set; }
            public CommandOption ModeF { get; set; }
        }

        private static CommonArgs AddCommonArgs(CommandLineApplication cmd)
        {
            cmd.HelpOption("-?|-h|--help");

            return new CommonArgs
            {
                Port = cmd.Argument<string>("port", "Serial port name which nooLite adapter connected to."),
                Channel = cmd.Argument<byte>("channel", "Target channel"),
                ModeF = cmd.Option<byte>("-f", "Set the nooLite-F mode.", CommandOptionType.NoValue)
            };
        }

        static void Invoke(CommonArgs args, Action<MTRFXXAdapter, byte> action, Action<MTRFXXAdapter, byte> actionF)
        {
            using (var adapter = new MTRFXXAdapter(args.Port.ParsedValue))
            {
                adapter.Open();
                adapter.ExitServiceMode();


                if (args.ModeF.HasValue())
                {
                    actionF(adapter, args.Channel.ParsedValue);
                }
                else
                {
                    action(adapter, args.Channel.ParsedValue);
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

            app.Command("bind", BindCommand);
            app.Command("unbind", UnbindCommand);
            app.Command("on", OnCommand);
            app.Command("off", OffCommand);
            app.Command("switch", SwitchCommand);

            app.OnExecute(() => { app.ShowHelp(); });

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

        private static void BindCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "123";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.Bind(c), (a, c) => a.BindF(c)));
        }

        private static void UnbindCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "123";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.Unbind(c), (a, c) => a.UnbindF(c)));
        }

        private static void OnCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "123";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.On(c), (a, c) => a.OnF(c)));
        }

        private static void OffCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "123";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.Off(c), (a, c) => a.OffF(c)));
        }

        private static void SwitchCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "123";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.Switch(c), (a, c) => a.SwitchF(c)));
        }
    }
}