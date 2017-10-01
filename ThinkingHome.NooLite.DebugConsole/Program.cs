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
            //using (var adapter = new MTRFXXAdapter("/dev/tty.usbserial-AI04XT35"))
            using (var adapter = new MTRFXXAdapter("/dev/tty.usbserial-AL00HDFI"))
            {
                adapter.Connect += AdapterOnConnect;
                adapter.Disconnect += AdapterOnDisconnect;

                adapter.ReceiveData += AdapterOnReceiveData;
                adapter.ReceiveMicroclimateData += AdapterOnReceiveMicroclimateData;

                adapter.Error += AdapterOnError;

//                Console.WriteLine("open");
//                adapter.Open();
//                Console.ReadKey();
//
//                Console.WriteLine("exit service mode");
//                adapter.ExitServiceMode();
//                Console.ReadKey();
//
//                Console.WriteLine("close");
//                adapter.Close();
//                Console.ReadKey();

                Console.WriteLine("open");
                adapter.Open();
                Console.ReadKey();

                Console.WriteLine("exit service mode");
                adapter.ExitServiceMode();
                Console.ReadKey();


//                Console.WriteLine("bind");
//                adapter.BindF(13);
//
//                Console.ReadKey();

//                Console.WriteLine("bind");
//                adapter.Bind(Mode.NooLiteF, 13);
//
//                Console.ReadKey();

//
//                Console.WriteLine("unbind");
//                adapter.Unbind(Mode.NooLiteF, 13);
//
//                Console.ReadKey();

//                Console.WriteLine("on");
//                adapter.OnF(13, 1594);
//
//                Console.ReadKey();
//
//                Console.WriteLine("off");
//                adapter.OffF(13, 1594);
//
//                Console.ReadKey();
//
//                Console.WriteLine("on");
//                adapter.OnF(13, 2405);
//
//                Console.ReadKey();
//
//                Console.WriteLine("off");
//                adapter.OffF(13, 2405);
//
//                Console.ReadKey();


                Console.WriteLine("bind: start");
                adapter.BindStart(13);

                Console.ReadKey();

                Console.WriteLine("bind: stop");
                adapter.BindStop();
            }
        }

        private static void AdapterOnReceiveMicroclimateData(object o, MicroclimateData result)
        {
            Console.WriteLine(result);
        }

        private static void AdapterOnError(object obj, Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        private static void AdapterOnDisconnect(object obj)
        {
            Console.WriteLine("disconnect");
        }

        private static void AdapterOnConnect(object obj)
        {
            Console.WriteLine("connect");
        }

        private static void AdapterOnReceiveData(object obj, ReceivedData result)
        {
            //var msg = string.Join("=", bytes.Select(b => b.ToString()));
            Console.WriteLine(result);
        }
    }
}