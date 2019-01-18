using System;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Threading;
using Iot.Device.Lcm1602c;

namespace LCDDisplay
{
    class Program
    {
        static void Main(string[] args)
        {
            int[] dataPins = { 6, 16, 20, 21 };
            int registerSelectPin = 18;
            int enablePin = 5;
            DateTime xmas = new DateTime(2019, 12, 25);
            CancellationTokenSource cts = new CancellationTokenSource();
            using (var lcd = new Lcm1602c(registerSelectPin, enablePin, dataPins))
            {
                lcd.Clear(); //Clear in case there was a previous program that left some text on the screen
                lcd.Begin(16, 2); //Initialize the lcd to use 2 rows, each with 16 characters.

                lcd.Print("X-Mas Countdown"); //Print string on first row.

                Console.CancelKeyPress += (o, e) => // Add handler for when the program should be terminated.
                {
                    cts.Cancel();
                };

                while (!cts.Token.IsCancellationRequested) // Loop until Ctr-C is pressed.
                {
                    lcd.SetCursor(0, 1);
                    TimeSpan countdown = xmas - DateTime.Now;
                    lcd.Print($"");
                }
            }
        }
    }
}
