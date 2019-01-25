using System;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Device.I2c;
using System.Device.I2c.Drivers;
using System.Threading;
using Iot.Device.Lcm1602c;
using Iot.Device.Mcp23xxx;

namespace LCDDisplay
{
    class Program
    {
        static void Main(string[] args)
        {
            // int[] dataPins = { 12, 11, 10, 9 };
            // int registerSelectPin = 15;
            // int enablePin = 13;
            // int readAndWritePin = 14;
            // DateTime xmas = new DateTime(2019, 12, 25);
            // CancellationTokenSource cts = new CancellationTokenSource();
            // using (var lcd = new Lcm1602c(registerSelectPin, enablePin, dataPins))
            // {
            //     lcd.Clear(); //Clear in case there was a previous program that left some text on the screen
            //     lcd.Begin(16, 2); //Initialize the lcd to use 2 rows, each with 16 characters.

            //     lcd.Print("X-Mas Countdown"); //Print string on first row.

            //     Console.CancelKeyPress += (o, e) => // Add handler for when the program should be terminated.
            //     {
            //         cts.Cancel();
            //     };

            //     while (!cts.Token.IsCancellationRequested) // Loop until Ctr-C is pressed.
            //     {
            //         lcd.SetCursor(0, 1);
            //         TimeSpan countdown = xmas - DateTime.Now;
            //         lcd.Print($"");
            //     }
            // }

            UnixI2cDevice i2CDevice = new UnixI2cDevice(new I2cConnectionSettings(1, 0x21));
            Mcp23008 mcpDevice = new Mcp23008(i2CDevice);
            int[] dataPins = { 3, 4, 5, 6 };
            int registerSelectPin = 1;
            int enablePin = 2;
            using (mcpDevice)
            using (Lcm1602c lcd = new Lcm1602c(mcpDevice, registerSelectPin, -1, enablePin, dataPins))
            {
                lcd.Clear();
                lcd.Begin(16, 2);

                lcd.Print("Hello World");
            }
        }
    }
}
