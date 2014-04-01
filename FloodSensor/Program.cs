using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using FloodSensor.NrpeCheckers.UpTime;
using FloodSensor.NrpeServer;
using FloodSensor.Util;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using Watchdog = FloodSensor.Util.Watchdog;


namespace FloodSensor
{
    public class Program
    {
        /// <summary>
        /// A stop for the program was requested by someone (for now, the user pressing the onboard button).
        /// </summary>
        public static bool StopRequested = false;

        /// <summary>
        /// Number of milliseconds before the board will reboot itself.
        /// </summary>
        public const int InactivityTimeout = 60 * UpTimeCheck.SecondsPerMinute * UpTimeCheck.MillisecondsPerSecond;
        //public const int InactivityTimeout = 30 * UpTimeCheck.MillisecondsPerSecond;
        /// <summary>
        /// How often to check for Watchdog expiration
        /// </summary>
        public const int InactivityCheckInterval = 5 * UpTimeCheck.MillisecondsPerSecond;
        /// <summary>
        /// This watchdog will reboot the board if it isn't tickled regularly by incoming requests. The assumption is
        /// your service checks will be at regular intervals that are shorter than the inactivity timeout, and that if this
        /// fails to happen the network stack may be down.
        /// </summary>
        public static Watchdog InactivityWatchdog = new Watchdog(InactivityTimeout, InactivityCheckInterval);

        public static void Main()
        {
            Debug.Print("Starting Main()");

            // The onboard button will perform a reset, and reboot the device. See below.
            using (var onboardButton = new InterruptPort(Pins.ONBOARD_SW1, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeHigh))
            {
                // Button event handler 
                onboardButton.OnInterrupt += button_OnInterrupt;

                try
                {
                    MainLoop();
                }
                catch (SocketException se)
                {
                    Debug.Print("Caught SocketException in main loop: " + se.Message);
                    Debug.Print("Error code: " + se.ErrorCode);
                    StopRequested = false;
                }
                catch (Exception e)
                {
                    Debug.Print("Caught exception in main loop: " + e.Message);
                    StopRequested = false;
                }
                Debug.Print("Stop requested: " + StopRequested);

                // We could just restart the MainLoop, but (while testing under unrealistically high load) I found that 
                // the Netduino network stack may have just up and died at this point. When this happens the the board 
                // will be unresponsive to pings, yet I still have threads running and a responsive debuggable board 
                // under Visual Studio.
                // 
                // So instead, here we do a hard reboot on the whole device. 
                // 
                // Not only will this end any debugging session, it just seems flat out wrong, but I'm far from the 
                // first to resort to it:
                //
                // http://forums.netduino.com/index.php?/topic/4030-netduino-plus-network-dies/?hl=%2Breboot+%2Bnetduino#entry49132
                // http://forums.netduino.com/index.php?/topic/3523-watchdog-redeux-available-in-42/
                // http://forums.netduino.com/index.php?/topic/8903-software-reboot-for-netduino-plus/#entry49735
                // http://forums.netduino.com/index.php?/topic/10550-how-hard-can-i-expect-to-lean-on-network-activity-for-netduino-plus-2-sample-code-attached/#entry56795
                //
                // What a pity! All help appreciated.                     
                HardReboot.HardRebootNetduino();
            }
            Debug.Print("Exiting Main()");
        }



        private static void MainLoop()
        {
            Debug.Print("Starting MainLoop()");
            StopRequested = false;

            using (var nrpeServer = new TinyNrpeServer())
            {
                // Start up NRPE server in separate thread
                var nrpeThread = new Thread(nrpeServer.RunServer);
                nrpeThread.Start();

                // Wait for TinyNRPE server to tell us it is shutting down
                nrpeServer.TinyNrpeServerIsShuttingDown.WaitOne();
                StopRequested = nrpeServer.NrpeServerStoppedNormally;
                Debug.Print("TinyNrpeServer exited. Stopped normally (was requested): " + StopRequested);
            }

            Debug.Print("Exiting MainLoop()");
        }

        /// <summary>
        /// Called when button is pressed
        /// </summary>
        static void button_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            Debug.Print("Button pressed");
            Debug.Print("Trying to stop NRPE server");
            TinyNrpeServer.StopNrpeServer = true;
        }
        
    }
}



