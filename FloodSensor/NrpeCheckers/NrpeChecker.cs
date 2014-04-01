using System.Collections;
using FloodSensor.NrpeCheckers.Flood;
using FloodSensor.NrpeCheckers.Temperature;
using FloodSensor.NrpeCheckers.UpTime;
using FloodSensor.NrpeServer;

namespace FloodSensor.NrpeCheckers
{
    /// <summary>
    /// Switching class that delegates specific queries to their specific responses
    /// </summary>
    class NrpeChecker
    {
        public static NrpeMessage ProcessQuery(NrpeMessage nrpeMessageQuery)
        {
            NrpeMessage queryResponse;
            var checkType = nrpeMessageQuery.BufferAsString.ToLower();
            switch (checkType)
            {
                case "check_flood":
                    queryResponse = new FloodCheck().DoCheck();
                    break;
                case "check_temp":
                    queryResponse = new TempCheck().DoCheck();
                    break;
                case "check_uptime":
                    queryResponse = new UpTimeCheck().DoCheck();
                    break;
                default:
                    queryResponse = new NrpeMessage(NrpeMessage.NrpePacketVersion.Version2,
                        NrpeMessage.NrpePacketType.PacketResponse, 
                        NrpeMessage.NrpeResultState.Unknown,
                        "UNKNOWN - message type " + checkType + " not recognized.",
                        new Hashtable());
                    break;

            }
            return queryResponse;
        }

    }
}