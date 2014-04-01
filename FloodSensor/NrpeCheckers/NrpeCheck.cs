using System.Collections;
using FloodSensor.NrpeServer;

namespace FloodSensor
{
    /// <summary>
    /// An abstract NRPE Check. Any particular check inherits from this.
    /// </summary>
    public abstract class NrpeCheck
    {
        public NrpeMessage DoCheck()
        {
            string statusString;
            Hashtable performanceData;
            var resultState = GetStatus(out statusString, out performanceData);
            var fullResultString = GetFullResultString(resultState, statusString);

            var queryResponse = new NrpeMessage(NrpeMessage.NrpePacketVersion.Version2,
                NrpeMessage.NrpePacketType.PacketResponse,
                resultState,
                fullResultString,
                performanceData);
            return queryResponse;
        }

        private static string GetFullResultString(NrpeMessage.NrpeResultState resultState, string statusString)
        {
            string resultPrefixString;
            switch (resultState)
            {
                case NrpeMessage.NrpeResultState.Ok:
                    resultPrefixString = "OK";
                    break;
                case NrpeMessage.NrpeResultState.Warning:
                    resultPrefixString = "WARNING";
                    break;
                case NrpeMessage.NrpeResultState.Critical:
                    resultPrefixString = "CRITICAL";
                    break;
                case NrpeMessage.NrpeResultState.Unknown:
                default:
                    resultPrefixString = "UNKNOWN";
                    break;
            }

            return resultPrefixString + " - " + statusString;
        }

        /// <summary>
        /// Get the current state of this check
        /// </summary>
        /// <param name="statusString"> Returns string with current status ("Everything is hunky dory -- no problem detected!")</param>
        /// <param name="performanceData">Key/Value pairs of performance data, if any</param>
        /// <returns>Gets the result state for this check</returns>
        public abstract NrpeMessage.NrpeResultState GetStatus(out string statusString, out Hashtable performanceData);

    }
}