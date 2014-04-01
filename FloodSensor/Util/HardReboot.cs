using System.Threading;
using FloodSensor.NrpeServer;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace FloodSensor.Util
{
    class HardReboot
    {
        public static void HardRebootNetduino()
        {
            const int rebootInterval = 5;
            for (int i = 0; i < 5; i++)
            {
                var remainingSecondsUntilReboot = rebootInterval - i;
                Debug.Print("Rebooting Netduino in " + remainingSecondsUntilReboot + " seconds..");
                Thread.Sleep(1000);
            }

            Debug.Print("Rebooting Netduino!");
            LedFlasher.FlashLed(4, 100);

            // The lock is a way to wait for the LED to be done flashing.
            lock (LedFlasher.LedFlashLockObj)
            {
                // You will lose any debug session here, unfortunately!
                PowerState.RebootDevice(false);
            }
        }

    }
}
