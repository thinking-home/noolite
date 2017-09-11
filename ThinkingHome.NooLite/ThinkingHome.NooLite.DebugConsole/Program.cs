using System;
using System.Linq;
using System.Threading;
using ThinkingHome.NooLite.Ports;

namespace ThinkingHome.NooLite.DebugConsole
{
    class Program
    {
        static void Main(string[] args)
        {
//            foreach (var name in SerialPort.GetPortNames())
//            {
//                Console.WriteLine(name);
//            }

            using (var adapter = new MTRFXXAdapter("/dev/tty.usbserial-AI04XT35"))
            {
                adapter.DataReceived += AdapterOnDataReceived;
                
                adapter.Open();
                
                adapter.SendCommand(MTRFXXMode.Service, MTRFXXAction.SendCommand, 0, MTRFXXCommand.Off);
                
                Thread.Sleep(200);
                
                adapter.SendCommand(MTRFXXMode.Service, MTRFXXAction.SendCommand, 0, MTRFXXCommand.On, MTRFXXRepeatCount.Three);
                
                Thread.Sleep(200);
            }
        }

        private static void AdapterOnDataReceived(object o, byte[] bytes)
        {
            var msg = string.Join("=", bytes.Select(b => b.ToString()));
            Console.WriteLine($"R: {msg}");
        }
    }
}