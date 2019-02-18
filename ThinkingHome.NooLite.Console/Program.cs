using System;
using System.Reflection;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using ThinkingHome.NooLite.Ports;

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
                Channel = cmd.Argument<byte>("channel", "Adapter channel in which need to send the command."),
                ModeF = cmd.Option<byte>("-f", "Switch the adapter into noolite-F mode.", CommandOptionType.NoValue)
            };
        }

        static void Invoke(CommonArgs args, Action<MTRFXXAdapter, byte> action, Action<MTRFXXAdapter, byte> actionF)
        {
            using (var adapter = new MTRFXXAdapter(args.Port.ParsedValue))
            {
                adapter.Open();
                adapter.ExitServiceMode();
                Thread.Sleep(50);

                if (args.ModeF.HasValue())
                {
                    actionF(adapter, args.Channel.ParsedValue);
                }
                else
                {
                    action(adapter, args.Channel.ParsedValue);
                }

                Thread.Sleep(100);
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

            app.Command("ports", PortsCommand);

            app.Command("bind", BindCommand);
            app.Command("bindstart", BindStartCommand);
            app.Command("bindstop", BindStopCommand);
            app.Command("unbind", UnbindCommand);
            app.Command("on", OnCommand);
            app.Command("off", OffCommand);
            app.Command("switch", SwitchCommand);

            app.Command("set-brightness", SetBrightnessCommand);
            app.Command("save-preset", SavePresetCommand);
            app.Command("load-preset", LoadPresetCommand);
            app.Command("change-color", ChangeColorCommand);
            app.Command("set-color", SetColorCommand);

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

        private static void PortsCommand(CommandLineApplication cmd)
        {
            cmd.HelpOption("-?|-h|--help");
            cmd.Description = "Display the list of the serial ports on this computer.";
            cmd.OnExecute(() =>
            {
                System.Console.WriteLine("Serial port list:");

                foreach (var portName in SerialPort.GetPortNames())
                {
                    System.Console.WriteLine($"- {portName}");
                }
            });
        }

        private static void BindCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "Binds the specified adapter channel to the nooLite power unit.";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.Bind(c), (a, c) => a.BindF(c)));
        }

        private static void BindStartCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "Enter bind mode to pair nooLite sensor.";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.BindStart(c), null));
        }

        private static void BindStopCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "Disable bind mode to pair nooLite sensor.";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.BindStop(), null));
        }

        private static void UnbindCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "Unbinds the specified adapter channel from the nooLite power unit.";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.Unbind(c), (a, c) => a.UnbindF(c)));
        }

        private static void OnCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "Turns on the power units in the specified adapter channel.";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.On(c), (a, c) => a.OnF(c)));
        }

        private static void OffCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "Turns off the power units in the specified adapter channel.";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.Off(c), (a, c) => a.OffF(c)));
        }

        private static void SwitchCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "Inverts state of the power units in the specified adapter channel.";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.Switch(c), (a, c) => a.SwitchF(c)));
        }

        private static void SetBrightnessCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);
            var brightness = cmd.Argument<byte>("brightness", "brightness level (0..255)");

            cmd.Description = "Sets brightness level in the specified adapter channel.";
            cmd.OnExecute(() => Invoke(args,
                (a, c) => a.SetBrightness(c, brightness.ParsedValue),
                (a, c) => a.SetBrightnessF(c, brightness.ParsedValue)));
        }

        private static void SavePresetCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "Saves current state of the power units in the specified adapter channel.";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.SavePreset(c), (a, c) => a.SavePresetF(c)));
        }

        private static void LoadPresetCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "Loads the saved state of the power units in the specified adapter channel.";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.LoadPreset(c), (a, c) => a.LoadPresetF(c)));
        }

        private static void ChangeColorCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            cmd.Description = "Changes LED strip light color in the specified adapter channel to another predefined color.";
            cmd.OnExecute(() => Invoke(args, (a, c) => a.ChangeLedColor(c), (a, c) => a.ChangeLedColorF(c)));
        }

        private static void SetColorCommand(CommandLineApplication cmd)
        {
            var args = AddCommonArgs(cmd);

            var colorR = cmd.Argument<byte>("red", "Red color level (0..255)");
            var colorG = cmd.Argument<byte>("green", "Green color level (0..255)");
            var colorB = cmd.Argument<byte>("blue", "Blue color level (0..255)");

            cmd.Description = "Changes LED strip light color in the specified adapter channel to specified color.";
            cmd.OnExecute(() => Invoke(args,
                (a, channel) => a.SetLedColor(channel, colorR.ParsedValue, colorG.ParsedValue, colorB.ParsedValue),
                (a, channel) => a.SetLedColorF(channel, colorR.ParsedValue, colorG.ParsedValue, colorB.ParsedValue)));
        }
    }
}