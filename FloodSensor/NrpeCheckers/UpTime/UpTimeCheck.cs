using System;
using System.Collections;
using System.Diagnostics;
using FloodSensor.NrpeServer;
using Microsoft.SPOT;

namespace FloodSensor.NrpeCheckers.UpTime
{
    /// <summary>
    /// Check to keep track of uptime for the Netduino since the last hard reboot
    /// </summary>
    public class UpTimeCheck : NrpeCheck
    {
        /// <summary>
        /// One uptime for the entire device, therefore this is static.
        /// </summary>
        public static Stopwatch UpTimer = Stopwatch.StartNew();

        public const int MillisecondsPerSecond = 1000;
        public const int SecondsPerMinute = 60;
        public const int MinutesPerHour = 60;    

        public override NrpeMessage.NrpeResultState GetStatus(out string statusString, out Hashtable performanceData)
        {
            performanceData = new Hashtable();

            // Throw in free memory while we're at it
            var freeMemory = Debug.GC(false); 
            performanceData.Add("free_memory", freeMemory);
       
            var uptimeInTicks = TimeSpan.TicksPerMillisecond * UpTimer.ElapsedMilliseconds;
            var upTimeSpan = new TimeSpan(uptimeInTicks);

            statusString = "Uptime: " + upTimeSpan.ToString() + " Free memory: " + freeMemory;            

            var uptimeInSeconds = UpTimer.ElapsedMilliseconds / MillisecondsPerSecond;
            var uptimeInMinutes = uptimeInSeconds / SecondsPerMinute;
            var uptimeInHours = uptimeInMinutes / MinutesPerHour;

            performanceData.Add("uptime_in_seconds", uptimeInSeconds);
            performanceData.Add("uptime_in_minutes", uptimeInMinutes);
            performanceData.Add("uptime_in_hours", uptimeInHours);

            // Always Ok.
            return NrpeMessage.NrpeResultState.Ok;
        }

        public static long GetElapsedMinutes()
        {
            var uptimeInSeconds = UpTimer.ElapsedMilliseconds / MillisecondsPerSecond;
            var uptimeInMinutes = uptimeInSeconds / SecondsPerMinute;
            return uptimeInMinutes;
        }

    }
}
