using System;
using System.IO.Ports;
using System.Threading;
using ThinkingHome.NooLite.Internal;

namespace ThinkingHome.NooLite.DebugConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var name in SerialPort.GetPortNames())
            {
                Console.WriteLine(name);
            }

            // return;
            //using (var adapter = new MTRFXXAdapter("/dev/tty.usbserial-AI04XT35"))
            using var adapter = new MTRFXXAdapter("/dev/tty.usbserial-AL00HDFI");

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
            Thread.Sleep(100);

            Console.WriteLine("exit service mode");
            adapter.ExitServiceMode();
            Thread.Sleep(100);


            // Console.WriteLine("bind");
            // Console.ReadKey();
            //
            // adapter.Bind(2);

            Console.WriteLine("on");
            adapter.OnF(13);
            Thread.Sleep(1500);
            Console.WriteLine("off");
            adapter.OffF(13);
            Thread.Sleep(1500);
            Console.WriteLine("on");
            adapter.OnF(13);
            Thread.Sleep(500);
            Console.WriteLine("off");
            adapter.OffF(13);
            Thread.Sleep(500);
            Console.WriteLine("on");
            adapter.OnF(13);
            Thread.Sleep(500);
            Console.WriteLine("off");
            adapter.OffF(13);
            Thread.Sleep(500);
            Console.WriteLine("done");

            for (byte ch = 0; ch < 64; ch++)
            {
                Console.WriteLine($@"clear: {ch}");

                // adapter.ClearChannel(ch);
                // adapter.Unbind(ch);
                // adapter.UnbindF(ch);
            }
            // //
            // return;

            // Console.WriteLine("prepare bind");
            // Console.ReadKey();
            // adapter.BindF(1);

            // Console.WriteLine("bind");
            // adapter.BindF(13);
            //
            // Console.ReadKey();
            //
            // Console.WriteLine("bind");
            // adapter.Bind(Mode.NooLiteF, 13);
            //
            // Console.ReadKey();

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
//                 Console.WriteLine("on");
//                 adapter.OnF(13, 33347);
//
//                 Console.ReadKey();
// //
//                 Console.WriteLine("off");
//                 adapter.OffF(13, 33347 );

            // Console.ReadKey();


            Console.WriteLine("switch on");
            adapter.OnF(0);

            Console.ReadKey();

            Console.WriteLine("switch off");
            adapter.OnF(0);
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
            Console.WriteLine("data:");
            Console.WriteLine(result);
        }
    }
}