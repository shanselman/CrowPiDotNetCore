using System;
using System.Threading;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;

namespace rpitest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            GpioController controller = new GpioController(PinNumberingScheme.Board);
            var pin = 37;
            var lightTime = 300;
            var dimTime = 300;

            controller.OpenPin(pin, PinMode.Output);
            try
            {
                while (true)
                {
                    Console.WriteLine($"Light for {lightTime}ms");
                    controller.Write(pin, PinValue.High);
                    Thread.Sleep(lightTime);
                    Console.WriteLine($"Dim for {dimTime}ms");
                    controller.Write(pin, PinValue.Low);
                    Thread.Sleep(dimTime);
                }
            }
            finally
            {
                controller.ClosePin(pin);
            }
        }
    }
}
