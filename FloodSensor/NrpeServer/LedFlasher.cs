using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace FloodSensor.NrpeServer
{
    class LedFlasher
    {
        public const int DefaultLedFlashTime = 200;
        public static OutputPort Led = new OutputPort(Pins.ONBOARD_LED, false);
        public static readonly object LedFlashLockObj = new object();

        public static void FlashLed(int pulses, int lengthOfOnPulseInMilliseconds = DefaultLedFlashTime, int lengthOfOffPulseInMilliseconds = DefaultLedFlashTime)
        {
            var ledFlashThread = new Thread(() =>
            {
                lock (LedFlashLockObj)
                {
                    for (int pulseCounter = 0; pulseCounter < pulses; pulseCounter++)
                    {
                        //Debug.Print("Pulse " + pulseCounter + " - Flashing LED on for " + lengthOfOnPulseInMilliseconds);
                        Led.Write(true);
                        Thread.Sleep(lengthOfOnPulseInMilliseconds);
                        //Debug.Print("Pulse " + pulseCounter + " - Flashing LED off for " + lengthOfOffPulseInMilliseconds);
                        Led.Write(false);
                        Thread.Sleep(lengthOfOffPulseInMilliseconds);
                    }
                    //Debug.Print("LED Flash thread exiting");          
                }
            });
            ledFlashThread.Start();
        }
    }
}
