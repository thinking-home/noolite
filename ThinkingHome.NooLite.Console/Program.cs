using System;
using System.IO.Ports;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace ThinkingHome.NooLite.Console;

internal class Program
{
    private class CommonArgs
    {
        public CommandArgument<string> Port { get; set; }
        public CommandArgument<byte> Channel { get; set; }
        public CommandOption ModeF { get; set; }
    }

    private static CommandArgument<string> AddPortArg(CommandLineApplication cmd)
    {
        cmd.HelpOption("-?|-h|--help");
        cmd.OnValidationError(result =>
        {
            System.Console.Error.WriteLine(result.ErrorMessage);
            cmd.ShowHelp();

            return 1;
        });

        return cmd.Argument<string>("port", "Serial port name which nooLite adapter connected to.").IsRequired();
    }

    private static CommandArgument<byte> AddChannelArg(CommandLineApplication cmd)
    {
        return cmd.Argument<byte>("channel", "Adapter channel in which need to send the command.").IsRequired();
    }

    private static CommonArgs AddCommonArgs(CommandLineApplication cmd)
    {
        var port = AddPortArg(cmd);
        var channel = AddChannelArg(cmd);
        var modeF = cmd.Option("-f", "Switch the adapter into noolite-F mode.", CommandOptionType.NoValue);

        return new CommonArgs { Port = port, Channel = channel, ModeF = modeF };
    }

    private static async Task InvokeAsync(string portName, Action<MTRFXXAdapter> action)
    {
        using (var adapter = new MTRFXXAdapter(portName))
        {
            // ошибки подключения и приёма библиотека отдаёт событием, а не исключением
            Exception failure = null;
            adapter.Error += (_, ex) => Interlocked.CompareExchange(ref failure, ex, null);

            adapter.Open();

            // без этой проверки причина отказа осталась бы в событии, а наружу вышло бы
            // невнятное "The port is closed" от следующей команды
            if (!adapter.IsOpened)
                throw Volatile.Read(ref failure)
                      ?? new InvalidOperationException($"Can not open the port '{portName}'.");

            adapter.ExitServiceMode();
            await Task.Delay(50);

            action(adapter);

            // пауза даёт адаптеру отправить команду в эфир, FlushAndCloseAsync закрывает порт,
            // дождавшись доставки принятого за это время (Dispose ниже отбросил бы остаток)
            await Task.Delay(100);
            await adapter.FlushAndCloseAsync();

            // команда уже ушла, поэтому ошибка приёма - предупреждение, а не отказ
            var error = Volatile.Read(ref failure);

            if (error != null) System.Console.Error.WriteLine(error.Message);
        }
    }

    private static Task Invoke(CommonArgs args, Action<MTRFXXAdapter, byte> action, Action<MTRFXXAdapter, byte> actionF)
    {
        var channel = args.Channel.ParsedValue;
        var send = args.ModeF.HasValue() ? actionF : action;

        return InvokeAsync(args.Port.ParsedValue, adapter => send(adapter, channel));
    }

    private static string GetVersion(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString();

        // SDK дописывает "+<sha>", если при сборке задан SourceRevisionId
        var index = version?.IndexOf('+') ?? -1;

        return index < 0 ? version : version.Substring(0, index);
    }

    private static async Task<int> Main(string[] args)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var app = new CommandLineApplication();
        app.Name = "noolite";
        app.FullName = "nooLite command line interface";
        app.HelpOption("-?|-h|--help");
        app.VersionOption("--version", GetVersion(assembly));
        app.ExtendedHelpText = "\nSee the details on https://github.com/thinking-home/noolite#readme.";

        app.Command("ports", PortsCommand);

        app.Command("bind", BindCommand);
        app.Command("unbind", UnbindCommand);
        app.Command("on", OnCommand);
        app.Command("off", OffCommand);
        app.Command("switch", SwitchCommand);
        app.Command("temporary-on", TemporaryOnCommand);

        app.Command("set-brightness", SetBrightnessCommand);
        app.Command("save-preset", SavePresetCommand);
        app.Command("load-preset", LoadPresetCommand);
        app.Command("change-color", ChangeColorCommand);
        app.Command("set-color", SetColorCommand);
        app.Command("switch-color-changing", SwitchColorChangingCommand);
        app.Command("change-color-mode", ChangeColorModeCommand);
        app.Command("change-color-speed", ChangeColorSpeedCommand);

        app.Command("bind-start", BindStartCommand);
        app.Command("bind-stop", BindStopCommand);
        app.Command("clear-channel", ClearChannelCommand);
        app.Command("clear-all", ClearAllCommand);

        app.OnExecute(() => { app.ShowHelp(); });

        try
        {
            return await app.ExecuteAsync(args);
        }
        catch (CommandParsingException e)
        {
            System.Console.Error.WriteLine(e.Message);
            app.ShowHelp();

            return 1;
        }
        catch (Exception e)
        {
            System.Console.Error.WriteLine(e.Message);

            return 1;
        }
    }

    private static void PortsCommand(CommandLineApplication cmd)
    {
        cmd.HelpOption("-?|-h|--help");
        cmd.Description = "Display the list of the serial ports on this computer.";
        cmd.OnExecute(() =>
        {
            System.Console.WriteLine("Serial port list:");

            foreach (var portName in SerialPort.GetPortNames()) System.Console.WriteLine($"- {portName}");
        });
    }

    private static void BindCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Binds the specified adapter channel to the nooLite power unit.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.Bind(c), (a, c) => a.BindF(c)));
    }

    private static void UnbindCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Unbinds the specified adapter channel from the nooLite power unit.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.Unbind(c), (a, c) => a.UnbindF(c)));
    }

    private static void OnCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Turns on the power units in the specified adapter channel.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.On(c), (a, c) => a.OnF(c)));
    }

    private static void OffCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Turns off the power units in the specified adapter channel.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.Off(c), (a, c) => a.OffF(c)));
    }

    private static void SwitchCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Inverts state of the power units in the specified adapter channel.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.Switch(c), (a, c) => a.SwitchF(c)));
    }

    private static void TemporaryOnCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);
        var interval = cmd.Argument<ushort>("interval", "Time interval in 5 second units (0..65535)").IsRequired();

        cmd.Description = "Turns on the power units in the specified adapter channel for the specified time interval.";
        cmd.OnExecuteAsync(_ => Invoke(args,
            (a, c) => a.TemporarySwitchOn(c, interval.ParsedValue),
            (a, c) => a.TemporarySwitchOnF(c, interval.ParsedValue)));
    }

    private static void SetBrightnessCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);
        var brightness = cmd.Argument<byte>("brightness", "brightness level (0..255)").IsRequired();

        cmd.Description = "Sets brightness level in the specified adapter channel.";
        cmd.OnExecuteAsync(_ => Invoke(args,
            (a, c) => a.SetBrightness(c, brightness.ParsedValue),
            (a, c) => a.SetBrightnessF(c, brightness.ParsedValue)));
    }

    private static void SavePresetCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Saves current state of the power units in the specified adapter channel.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.SavePreset(c), (a, c) => a.SavePresetF(c)));
    }

    private static void LoadPresetCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Loads the saved state of the power units in the specified adapter channel.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.LoadPreset(c), (a, c) => a.LoadPresetF(c)));
    }

    private static void ChangeColorCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Changes LED strip light color in the specified adapter channel to another predefined color.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.ChangeLedColor(c), (a, c) => a.ChangeLedColorF(c)));
    }

    private static void SetColorCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        var colorR = cmd.Argument<byte>("red", "Red color level (0..255)").IsRequired();
        var colorG = cmd.Argument<byte>("green", "Green color level (0..255)").IsRequired();
        var colorB = cmd.Argument<byte>("blue", "Blue color level (0..255)").IsRequired();

        cmd.Description = "Changes LED strip light color in the specified adapter channel to specified color.";
        cmd.OnExecuteAsync(_ => Invoke(args,
            (a, channel) => a.SetLedColor(channel, colorR.ParsedValue, colorG.ParsedValue, colorB.ParsedValue),
            (a, channel) => a.SetLedColorF(channel, colorR.ParsedValue, colorG.ParsedValue, colorB.ParsedValue)));
    }

    private static void SwitchColorChangingCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Switches the smooth color changing mode of the LED strip in the specified adapter channel.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.SwitchColorChanging(c), (a, c) => a.SwitchColorChangingF(c)));
    }

    private static void ChangeColorModeCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Changes the color mode of the LED strip in the specified adapter channel.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.ChangeLedColorMode(c), (a, c) => a.ChangeLedColorModeF(c)));
    }

    private static void ChangeColorSpeedCommand(CommandLineApplication cmd)
    {
        var args = AddCommonArgs(cmd);

        cmd.Description = "Changes the color changing speed of the LED strip in the specified adapter channel.";
        cmd.OnExecuteAsync(_ => Invoke(args, (a, c) => a.ChangeLedColorSpeed(c), (a, c) => a.ChangeLedColorSpeedF(c)));
    }

    private static void BindStartCommand(CommandLineApplication cmd)
    {
        var port = AddPortArg(cmd);
        var channel = AddChannelArg(cmd);

        cmd.Description = "Switches the adapter into the binding mode to bind a sensor or a remote control.";
        cmd.OnExecuteAsync(_ => InvokeAsync(port.ParsedValue, a => a.BindStart(channel.ParsedValue)));
    }

    private static void BindStopCommand(CommandLineApplication cmd)
    {
        var port = AddPortArg(cmd);

        cmd.Description = "Switches the adapter out of the binding mode.";
        cmd.OnExecuteAsync(_ => InvokeAsync(port.ParsedValue, a => a.BindStop()));
    }

    private static void ClearChannelCommand(CommandLineApplication cmd)
    {
        var port = AddPortArg(cmd);
        var channel = AddChannelArg(cmd);

        cmd.Description = "Removes all the sensors and the remote controls bound to the specified adapter channel.";
        cmd.OnExecuteAsync(_ => InvokeAsync(port.ParsedValue, a => a.ClearChannel(channel.ParsedValue)));
    }

    private static void ClearAllCommand(CommandLineApplication cmd)
    {
        var port = AddPortArg(cmd);

        cmd.Description = "Removes all the sensors and the remote controls bound to any channel of the adapter.";
        cmd.OnExecuteAsync(_ => InvokeAsync(port.ParsedValue, a => a.ClearAllChannels()));
    }
}
