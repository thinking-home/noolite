using System;
using System.IO.Ports;
using System.Threading;

namespace ThinkingHome.NooLite.DebugConsole;

internal class Program
{
    private const int RESPONSE_TIMEOUT = 1500;

    private const int RX_BINDING_TIMEOUT = 40000;

    private class Options
    {
        public string Port { get; set; }
        public byte Channel { get; set; }
        public bool ModeF { get; set; }
        public uint? DeviceId { get; set; }
        public byte Format { get; set; }
    }

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 1;
        }

        var mode = args[0].ToLowerInvariant();

        try
        {
            switch (mode)
            {
                case "ports":
                    return PortsMode();

                case "listen":
                    return ListenMode(ParseOptions(args, false));

                case "on":
                    return CommandMode(ParseOptions(args, true),
                        (a, o) => { if (o.ModeF) a.OnF(o.Channel, o.DeviceId); else a.On(o.Channel); });

                case "off":
                    return CommandMode(ParseOptions(args, true),
                        (a, o) => { if (o.ModeF) a.OffF(o.Channel, o.DeviceId); else a.Off(o.Channel); });

                case "switch":
                    return CommandMode(ParseOptions(args, true),
                        (a, o) => { if (o.ModeF) a.SwitchF(o.Channel, o.DeviceId); else a.Switch(o.Channel); });

                case "bind":
                    return CommandMode(ParseOptions(args, true),
                        (a, o) => { if (o.ModeF) a.BindF(o.Channel); else a.Bind(o.Channel); });

                case "unbind":
                    return CommandMode(ParseOptions(args, true),
                        (a, o) => { if (o.ModeF) a.UnbindF(o.Channel); else a.Unbind(o.Channel); });

                case "clear":
                    return CommandMode(ParseOptions(args, true), (a, o) => a.ClearChannel(o.Channel));

                case "clear-all":
                    return CommandMode(ParseOptions(args, false), (a, o) => a.ClearAllChannels());

                case "bind-rx":
                    return BindRxMode(ParseOptions(args, true));

                case "state":
                    // Read_State - команда двусторонней связи, режим F подразумевается
                    return CommandMode(ParseOptions(args, true),
                        (a, o) => a.ReadStateF(o.Channel, o.DeviceId, o.Format));

                default:
                    Console.Error.WriteLine($"unknown mode: {args[0]}");
                    ShowHelp();
                    return 1;
            }
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            ShowHelp();
            return 1;
        }
    }

    #region modes

    private static int PortsMode()
    {
        Console.WriteLine("serial port list:");

        foreach (var name in SerialPort.GetPortNames()) Console.WriteLine($"- {name}");

        return 0;
    }

    private static int ListenMode(Options options)
    {
        return WithAdapter(options, _ => WaitForInterrupt("listening for incoming packets"));
    }

    private static int CommandMode(Options options, Action<MTRFXXAdapter, Options> action)
    {
        return WithAdapter(options, adapter =>
        {
            action(adapter, options);
            Thread.Sleep(RESPONSE_TIMEOUT);
        });
    }

    private static int BindRxMode(Options options)
    {
        return WithAdapter(options, adapter =>
        {
            adapter.BindStart(options.Channel);
            WaitForInterrupt($"binding window is open for channel {options.Channel}", RX_BINDING_TIMEOUT);
            adapter.BindStop();
            Thread.Sleep(RESPONSE_TIMEOUT);
        });
    }

    #endregion

    #region adapter

    private static int WithAdapter(Options options, Action<MTRFXXAdapter> action)
    {
        using var adapter = new MTRFXXAdapter(options.Port);

        adapter.Connect += AdapterOnConnect;
        adapter.Disconnect += AdapterOnDisconnect;
        adapter.Error += AdapterOnError;
        adapter.ReceiveData += AdapterOnReceiveData;
        adapter.ReceiveMicroclimateData += AdapterOnReceiveMicroclimateData;
        adapter.ReceivePowerUnitState += AdapterOnReceivePowerUnitState;
        adapter.ReceiveStateFormatError += AdapterOnReceiveStateFormatError;

        Console.WriteLine($"open {options.Port}");
        adapter.Open();

        if (!adapter.IsOpened)
        {
            Console.Error.WriteLine($"can't open port {options.Port}");
            return 1;
        }

        Thread.Sleep(100);

        Console.WriteLine("exit service mode");
        adapter.ExitServiceMode();
        Thread.Sleep(100);

        action(adapter);

        Console.WriteLine("done");
        if (adapter.DroppedPacketsCount > 0)
            Console.WriteLine($"dropped packets: {adapter.DroppedPacketsCount}");

        return 0;
    }

    private static void WaitForInterrupt(string message, int timeout = Timeout.Infinite)
    {
        using var stop = new ManualResetEventSlim(false);

        void CancelHandler(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            stop.Set();
        }

        Console.CancelKeyPress += CancelHandler;

        try
        {
            Console.WriteLine($"{message}, press Ctrl+C to stop");
            stop.Wait(timeout);
        }
        finally
        {
            Console.CancelKeyPress -= CancelHandler;
        }
    }

    private static void AdapterOnConnect(object obj)
    {
        Console.WriteLine("connect");
    }

    private static void AdapterOnDisconnect(object obj)
    {
        Console.WriteLine("disconnect");
    }

    private static void AdapterOnError(object obj, Exception ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
    }

    private static void AdapterOnReceiveData(object obj, ReceivedData result)
    {
        Console.WriteLine($"data: {result}");
    }

    private static void AdapterOnReceiveMicroclimateData(object obj, MicroclimateData result)
    {
        Console.WriteLine($"microclimate: {result}");
    }

    private static void AdapterOnReceivePowerUnitState(object obj, PowerUnitStateData result)
    {
        Console.WriteLine($"power unit state: {result}");
    }

    private static void AdapterOnReceiveStateFormatError(object obj, StateFormatErrorData result)
    {
        Console.WriteLine($"state format error: {result}");
    }

    #endregion

    #region arguments

    private static Options ParseOptions(string[] args, bool channelRequired)
    {
        if (args.Length < 2) throw new ArgumentException("port name is required");

        var options = new Options { Port = args[1] };
        byte? channel = null;

        for (var i = 2; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "-f")
            {
                options.ModeF = true;
            }
            else if (arg == "--id")
            {
                if (++i >= args.Length) throw new ArgumentException("--id requires a value");
                if (!uint.TryParse(args[i], out var deviceId))
                    throw new ArgumentException($"invalid device id: {args[i]}");

                options.DeviceId = deviceId;
            }
            else if (arg == "--fmt")
            {
                if (++i >= args.Length) throw new ArgumentException("--fmt requires a value");
                if (!byte.TryParse(args[i], out var format))
                    throw new ArgumentException($"invalid format: {args[i]}");

                options.Format = format;
            }
            else if (channel == null)
            {
                if (!byte.TryParse(arg, out var value)) throw new ArgumentException($"invalid channel: {arg}");

                channel = value;
            }
            else
            {
                throw new ArgumentException($"unexpected argument: {arg}");
            }
        }

        if (channelRequired && channel == null) throw new ArgumentException("channel is required");

        options.Channel = channel ?? 0;

        return options;
    }

    private static void ShowHelp()
    {
        Console.WriteLine(@"
nooLite adapter debug console.

usage: <mode> [<port> [<channel>]] [-f] [--id <device id>] [--fmt <row>]

modes:
  ports                       display the list of the serial ports on this computer
  listen <port>               print all incoming packets until interrupted
  on <port> <channel>         turn on the power units in the channel
  off <port> <channel>        turn off the power units in the channel
  switch <port> <channel>     invert state of the power units in the channel
  bind <port> <channel>       bind the channel to the power unit
  unbind <port> <channel>     unbind the channel from the power unit
  bind-rx <port> <channel>    open the binding window for a sensor (RX mode)
  clear <port> <channel>      clear the channel cell
  clear-all <port>            clear the whole adapter memory
  state <port> <channel>      request the state of nooLite-F power units (Read_State; -f implied)

options:
  -f                          use the nooLite-F mode
  --id <device id>            send the command to the specified nooLite-F device (requires -f);
                              use '--id 0' to send the broadcast command
  --fmt <row>                 state table row to request with 'state' (default 0 - main info)

all modes except 'ports' print incoming packets while running; parsed power unit state
(Send_State, FMT 0) and state format errors (FMT 255) are printed separately.

examples:
  listen COM3
  on COM3 13 -f
  off COM3 13 -f --id 1594
  bind-rx COM3 2
  state COM3 0
  state COM3 0 --id 33347
  state COM3 0 --fmt 200");
    }

    #endregion
}
