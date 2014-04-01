using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.SPOT;

namespace FloodSensor.Util
{
    public class Watchdog
    {
        private readonly Stopwatch _rebootStopwatch;
        private Timer _watchdogCheckTimer;
        private readonly int _timeoutLength;

        /// <summary>      
        /// Creates a watchdog     
        /// </summary>
        /// <param name="timeoutLength">Number of milliseconds without Running check-in that must elapse before Watchdog reboot.</param>
        /// <param name="period">Number of milliseconds between each Timer check</param>      
        /// <param name="delay">Number of milliseconds before the watchdog starts running</param>    
        public Watchdog(int timeoutLength, int period = 1000, int delay = 0)
        {
            this._timeoutLength = timeoutLength;
            _watchdogCheckTimer = new Timer(_watchdog, null, delay, period);
            _rebootStopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Application calls this periodically to let the Watchdog know everything is all right.
        /// </summary>
        public void CheckIn()
        {
            Debug.Print("Program is checking in with Watchdog. Elapsed watchdog time: " + _rebootStopwatch.ElapsedTimespan.ToString());
            _rebootStopwatch.Reset();
            _rebootStopwatch.Start();
        }

        /// <summary>     
        /// The internal watchdog process  
        /// </summary>       
        /// <param name="state">Required by the Timer class, but not used</param>      
        private void _watchdog(object state)
        {
            var watchdogTimespan = new TimeSpan(0, 0, 0, 0, this._timeoutLength);

            string preString = "Watchdog Check. Elapsed watchdog time: " + _rebootStopwatch.ElapsedTimespan.ToString() + ". Expires after " + watchdogTimespan.ToString() + ". ";
            if (_rebootStopwatch.ElapsedMilliseconds > _timeoutLength)
            {
                Debug.Print(preString + "Watchdog expired; rebooting Netduino");
                _watchdogCheckTimer.Dispose();
                _rebootStopwatch.Stop();                
                HardReboot.HardRebootNetduino();
            }
            else
            {
                Debug.Print(preString + "Watchdog not yet expired.");
            }
        }
    }
}