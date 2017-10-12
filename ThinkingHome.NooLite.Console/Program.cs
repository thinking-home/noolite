using System;
using ThinkingHome.NooLite;

namespace ThinkingHome.NooLite.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var gateway = new ThinkingHome.NooLite.PR1132Gateway("192.168.2.3");

            foreach (var sensorData in gateway.LoadSensorData())
            {
                System.Console.WriteLine(string.Format("Temperateure {0}, Humidity {1}", sensorData.Temperature, sensorData.Humidity));
            }

            System.Console.ReadKey();
        }
    }
}