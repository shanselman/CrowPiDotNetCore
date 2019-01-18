// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Iot.Device.Lcm1602c
{
    /// <summary>
    /// Supports Lcm1602c LCD controller. Ported from: https://github.com/adafruit/Adafruit_Python_CharLCD/blob/master/Adafruit_CharLCD/Adafruit_CharLCD.py
    /// </summary>
    public class Lcm1602c : IDisposable
    {
        // When the display powers up, it is configured as follows:
        //
        // 1. Display clear
        // 2. Function set: 
        //    DL = 1; 8-bit interface data 
        //    N = 0; 1-line display 
        //    F = 0; 5x8 dot character font 
        // 3. Display on/off control: 
        //    D = 0; Display off 
        //    C = 0; Cursor off 
        //    B = 0; Blinking off 
        // 4. Entry mode set: 
        //    I/D = 1; Increment by 1 
        //    S = 0; No shift 
        //
        // Note, however, that resetting the device doesn't reset the LCD, so we
        // can't assume that its in that state when a sketch starts (and the
        // LiquidCrystal constructor is called).

        private readonly int _rsPin; // LOW: command.  HIGH: character.
        private readonly int _rwPin; // LOW: write to LCD.  HIGH: read from LCD.
        private readonly int _enablePin; // Activated by a HIGH pulse.
        private readonly int[] _dataPins;

        private GpioController _controller;

        private DisplayFlags _displayFunction;
        private DisplayFlags _displayControl;
        private DisplayFlags _displayMode;

        private byte _numLines;
        private readonly byte[] _rowOffsets;

        public Lcm1602c(int registerSelect, int enable, params int[] data)
            : this(registerSelect, -1, enable, data)
        {
            // Do nothing
        }

        public Lcm1602c(int registerSelect, int readWrite, int enable, params int[] data)
        {
            _rwPin = readWrite;
            _rsPin = registerSelect;
            _enablePin = enable;
            _dataPins = data;

            _rowOffsets = new byte[4];

            _displayFunction = DisplayFlags.LCD_1LINE | DisplayFlags.LCD_5x8DOTS;

            if (data.Length == 4)
            {
                _displayFunction |= DisplayFlags.LCD_4BITMODE;
            }
            else if (data.Length == 8)
            {
                _displayFunction |= DisplayFlags.LCD_8BITMODE;
            }
            else
            {
                throw new ArgumentException($"The length of the array given to parameter {nameof(data)} must be 4 or 8");
            }
            _controller = (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ?
                                new GpioController(PinNumberingScheme.Logical, new UnixDriver()) :
                                new GpioController(PinNumberingScheme.Logical, new Windows10Driver());
            _controller.OpenPin(_rsPin, PinMode.Input);
            if (_rwPin != -1)
                _controller.OpenPin(_rwPin, PinMode.Input);
            _controller.OpenPin(_enablePin, PinMode.Input);
            foreach (int i in _dataPins)
                _controller.OpenPin(i, PinMode.Input);
            // By default, initialize the display with one row and 16 characters.
            Begin(16, 1);
        }

        public void Dispose()
        {
            if (_controller != null)
            {
                _controller.Dispose();
                _controller = null;
            }
        }

        public void Begin(byte cols, byte lines, DisplayFlags dotSize = DisplayFlags.LCD_5x8DOTS)
        {
            if (lines > 1)
            {
                _displayFunction |= DisplayFlags.LCD_2LINE;
            }

            _numLines = lines;

            SetRowOffsets(0x00, 0x40, (byte)(0x00 + cols), (byte)(0x40 + cols));

            // for some 1 line displays you can select a 10 pixel high font
            if ((dotSize != DisplayFlags.LCD_5x8DOTS) && (lines == 1))
            {
                _displayFunction |= DisplayFlags.LCD_5x10DOTS;
            }

            _controller.SetPinMode(_rsPin, PinMode.Output);
            // we can save 1 pin by not using RW. Indicate by passing null instead of a pin
            if (_rwPin != -1)
            {
                _controller.SetPinMode(_rwPin, PinMode.Output);
            }
            _controller.SetPinMode(_enablePin, PinMode.Output);

            // Do this just once, instead of every time a character is drawn (for speed reasons).
            for (int i = 0; i < _dataPins.Length; ++i)
            {
                _controller.SetPinMode(_dataPins[i], PinMode.Output);
            }

            // SEE PAGE 45/46 FOR INITIALIZATION SPECIFICATION!
            // according to datasheet, we need at least 40ms after power rises above 2.7V
            // before sending commands. Arduino can turn on way before 4.5V so we'll wait 50
            DelayMicroseconds(50000);
            // Now we pull both RS and R/W low to begin commands
            _controller.Write(_rsPin, PinValue.Low);
            _controller.Write(_enablePin, PinValue.Low);

            if (_rwPin != -1)
            {
                _controller.Write(_rwPin, PinValue.Low);
            }

            //put the LCD into 4 bit or 8 bit mode
            if (_displayFunction.HasFlag(DisplayFlags.LCD_8BITMODE))
            {
                // this is according to the hitachi HD44780 datasheet
                // page 45 figure 23

                // Send function set command sequence
                Command((byte)Commands.LCD_FUNCTIONSET | (byte)_displayFunction);
                DelayMicroseconds(4500);  // wait more than 4.1ms

                // second try
                Command((byte)Commands.LCD_FUNCTIONSET | (byte)_displayFunction);
                DelayMicroseconds(150);

                // third go
                Command((byte)Commands.LCD_FUNCTIONSET | (byte)_displayFunction);
            }
            else
            {
                // this is according to the hitachi HD44780 datasheet
                // figure 24, pg 46

                // we start in 8bit mode, try to set 4 bit mode
                Write4bits(0x03);
                DelayMicroseconds(4500); // wait min 4.1ms

                // second try
                Write4bits(0x03);
                DelayMicroseconds(4500); // wait min 4.1ms

                // third go!
                Write4bits(0x03);
                DelayMicroseconds(150);

                // finally, set to 4-bit interface
                Write4bits(0x02);
            }

            // finally, set # lines, font size, etc.
            Command((byte)Commands.LCD_FUNCTIONSET | (byte)_displayFunction);

            // turn the display on with no cursor or blinking default
            _displayControl = DisplayFlags.LCD_DISPLAYON | DisplayFlags.LCD_CURSOROFF | DisplayFlags.LCD_BLINKOFF;
            Display();

            // clear it off
            Clear();

            // Initialize to default text direction (for romance languages)
            _displayMode = DisplayFlags.LCD_ENTRYLEFT | DisplayFlags.LCD_ENTRYSHIFTDECREMENT;
            // set the entry mode
            Command((byte)Commands.LCD_ENTRYMODESET | (byte)_displayMode);
        }

        private void SetRowOffsets(byte row0, byte row1, byte row2, byte row3)
        {
            _rowOffsets[0] = row0;
            _rowOffsets[1] = row1;
            _rowOffsets[2] = row2;
            _rowOffsets[3] = row3;
        }

        #region High Level Surface Area
        public void Clear()
        {
            Command((byte)Commands.LCD_CLEARDISPLAY);  // clear display, set cursor position to zero
            DelayMicroseconds(2000);  // this command takes a long time!
        }

        public void Home()
        {
            Command((byte)Commands.LCD_RETURNHOME);  // set cursor position to zero
            DelayMicroseconds(2000);  // this command takes a long time!
        }

        public void SetCursor(byte col, byte row)
        {
            if (row >= _rowOffsets.Length)
            {
                row = (byte)(_rowOffsets.Length - 1);    // we count rows starting w/0
            }
            if (row >= _numLines)
            {
                row = (byte)(_numLines - 1);    // we count rows starting w/0
            }

            Command((byte)Commands.LCD_SETDDRAMADDR | (col + _rowOffsets[row]));
        }

        // Turn the display on/off (quickly)
        public void NoDisplay()
        {
            _displayControl &= ~DisplayFlags.LCD_DISPLAYON;
            Command((byte)Commands.LCD_DISPLAYCONTROL | (byte)_displayControl);
        }

        public void Display()
        {
            _displayControl |= DisplayFlags.LCD_DISPLAYON;
            Command((byte)Commands.LCD_DISPLAYCONTROL | (byte)_displayControl);
        }

        // Turns the underline cursor on/off
        public void NoCursor()
        {
            _displayControl &= ~DisplayFlags.LCD_CURSORON;
            Command((byte)Commands.LCD_DISPLAYCONTROL | (byte)_displayControl);
        }

        public void Cursor()
        {
            _displayControl |= DisplayFlags.LCD_CURSORON;
            Command((byte)Commands.LCD_DISPLAYCONTROL | (byte)_displayControl);
        }

        // Turn on and off the blinking cursor
        public void NoBlink()
        {
            _displayControl &= ~DisplayFlags.LCD_BLINKON;
            Command((byte)Commands.LCD_DISPLAYCONTROL | (byte)_displayControl);
        }

        public void Blink()
        {
            _displayControl |= DisplayFlags.LCD_BLINKON;
            Command((byte)Commands.LCD_DISPLAYCONTROL | (byte)_displayControl);
        }

        // These commands scroll the display without changing the RAM
        public void ScrollDisplayLeft()
        {
            Command((byte)Commands.LCD_CURSORSHIFT | (byte)DisplayFlags.LCD_DISPLAYMOVE | (byte)DisplayFlags.LCD_MOVELEFT);
        }

        public void ScrollDisplayRight()
        {
            Command((byte)Commands.LCD_CURSORSHIFT | (byte)DisplayFlags.LCD_DISPLAYMOVE | (byte)DisplayFlags.LCD_MOVERIGHT);
        }

        // This is for text that flows Left to Right
        public void LeftToRight()
        {
            _displayMode |= DisplayFlags.LCD_ENTRYLEFT;
            Command((byte)Commands.LCD_ENTRYMODESET | (byte)_displayMode);
        }

        // This is for text that flows Right to Left
        public void RightToLeft()
        {
            _displayMode &= ~DisplayFlags.LCD_ENTRYLEFT;
            Command((byte)Commands.LCD_ENTRYMODESET | (byte)_displayMode);
        }

        // This will 'right justify' text from the cursor
        public void Autoscroll()
        {
            _displayMode |= DisplayFlags.LCD_ENTRYSHIFTINCREMENT;
            Command((byte)Commands.LCD_ENTRYMODESET | (byte)_displayMode);
        }

        // This will 'left justify' text from the cursor
        public void NoAutoscroll()
        {
            _displayMode &= ~DisplayFlags.LCD_ENTRYSHIFTINCREMENT;
            Command((byte)Commands.LCD_ENTRYMODESET | (byte)_displayMode);
        }

        // Allows us to fill the first 8 CGRAM locations
        // with custom characters
        public void CreateChar(byte location, params byte[] charmap)
        {
            if (charmap.Length != 8)
            {
                throw new ArgumentException(nameof(charmap));
            }

            location &= 0x7; // we only have 8 locations 0-7
            Command((byte)Commands.LCD_SETCGRAMADDR | (location << 3));

            for (int i = 0; i < 8; i++)
            {
                Write(charmap[i]);
            }
        }

        public void Print(string value)
        {
            for (int i = 0; i < value.Length; ++i)
            {
                Write(value[i]);
            }
        }
        #endregion // High Level Surface Area

        #region Mid Level Methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Command(int value)
        {
            Command((byte)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Command(byte value)
        {
            Send(value, PinValue.Low);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(char value)
        {
            Write((byte)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(byte value)
        {
            Send(value, PinValue.High);
        }
        #endregion // Mid Level Methods

        #region Low Level Methods
        // write either command or data, with automatic 4/8-bit selection
        private void Send(byte value, PinValue mode)
        {
            _controller.Write(_rsPin, mode);

            // if there is a RW pin indicated, set it low to Write
            if (_rwPin != -1)
            {
                _controller.Write(_rwPin, PinValue.Low);
            }

            if (_displayFunction.HasFlag(DisplayFlags.LCD_8BITMODE))
            {
                Write8bits(value);
            }
            else
            {
                Write4bits((byte)(value >> 4));
                Write4bits(value);
            }
        }

        private void PulseEnable()
        {
            _controller.Write(_enablePin, PinValue.Low);
            DelayMicroseconds(1);
            _controller.Write(_enablePin, PinValue.High);
            DelayMicroseconds(1);    // enable pulse must be >450ns
            _controller.Write(_enablePin, PinValue.Low);
            DelayMicroseconds(100);   // commands need > 37us to settle
        }

        private void Write4bits(byte value)
        {
            for (int i = 0; i < 4; i++)
            {
                DigitalWrite(_dataPins[i], value >> i);
            }

            PulseEnable();
        }

        private void Write8bits(byte value)
        {
            for (int i = 0; i < 8; i++)
            {
                DigitalWrite(_dataPins[i], value >> i);
            }

            PulseEnable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DigitalWrite(int pin, int value)
        {
            PinValue state = (value == 1) ? PinValue.High : PinValue.Low;
            _controller.Write(pin, state);
        }

        private static void DelayMicroseconds(int microseconds)
        {
            Stopwatch sw = Stopwatch.StartNew();
            long v = (microseconds * System.Diagnostics.Stopwatch.Frequency) / 1000000;
            while (sw.ElapsedTicks < v)
            {
                // Do nothing
            }
        }
        #endregion // Low Level Methods
    }
}