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
//
//            return;
            using (var adapter = new MTRFXXAdapter("/dev/tty.usbserial-AI04XT35"))
            {
                adapter.DataReceived += AdapterOnDataReceived;
                
                adapter.Open();

                Console.WriteLine("exit service mode");
                adapter.ExitServiceMode();

                Console.ReadKey();

                Console.WriteLine("bind");
                adapter.Bind(13);

                Console.ReadKey();
                
                
//                Console.WriteLine("unbind");
//                adapter.UnBind(13);
//
//                Console.ReadKey();

                Console.WriteLine("on");
                adapter.On(13);

                Console.ReadKey();

                Console.WriteLine("off");
                adapter.Off(13);

                Console.ReadKey();
                
                Console.WriteLine("on");
                adapter.On(13);    

                Console.ReadKey();

                Console.WriteLine("off");
                adapter.Off(13);

                Console.ReadKey();
                
                
//                Console.WriteLine("bind: start");
//                adapter.BindStart(13);
//
//                Console.ReadKey();
//                
//                Console.WriteLine("bind: stop");
//                adapter.BindStop();
            }
        }

        private static void AdapterOnDataReceived(object o, ReceivedData result)
        {
            //var msg = string.Join("=", bytes.Select(b => b.ToString()));
            Console.WriteLine(result);
        }
    }
}