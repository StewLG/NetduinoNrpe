using System;
using System.Collections;
using FloodSensor.NrpeServer;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace FloodSensor.NrpeCheckers.Flood
{
    public class FloodCheck : NrpeCheck
    {
        public override NrpeMessage.NrpeResultState GetStatus(out string statusString, out Hashtable performanceData)
        {
            performanceData = new Hashtable();

            using (var floodInput = new InputPort(Pins.GPIO_PIN_D10, true, Port.ResistorMode.Disabled))
            {                
                // I found that different sensors invert this. The "Fundino" I'm using is 1 == water.
                bool waterDetected = floodInput.Read();
                if (!waterDetected)
                {
                    statusString = "No water detected";
                    performanceData.Add("water_detected", 0);
                    return NrpeMessage.NrpeResultState.Ok;
                }

                statusString = "Water detected!";
                performanceData.Add("water_detected", 1);
                return NrpeMessage.NrpeResultState.Critical;
            }   
        }

    }
}
