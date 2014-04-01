using System.Collections;
using System.Threading;
using FloodSensor.NrpeServer;
using FloodSensor.Util;
using Microsoft.SPOT;
using SecretLabs.NETMF.Hardware.Netduino;

namespace FloodSensor.NrpeCheckers.Temperature
{
    public class TempCheck : NrpeCheck
    {
        private const int WarnCelsius = 35;
        private const int CriticalCelsius = 38;
        private const int MinCelsius = 0;
        private const int MaxCelsius = 100;

        // These humidity warnings are just total guesses. If you know better, please change them to suit yourself.
        private const int WarnHumidity = 70;
        private const int CriticalHumidity = 80;
        private const int MinHumidity = 0;
        private const int MaxHumidity = 100;
               
        public override NrpeMessage.NrpeResultState GetStatus(out string statusString, out Hashtable performanceData)
        {
            NrpeMessage.NrpeResultState resultState;
            using (var RHT03 = new Dht22Sensor(Pins.GPIO_PIN_D4, Pins.GPIO_PIN_D5, PullUpResistor.Internal))
            {
                ReadUntilNonZeroReadingObtained(RHT03);

                performanceData = new Hashtable();

                if (RHT03.Read())
                {
                    var temperatureCelsius = RHT03.Temperature;
                    var humidity = RHT03.Humidity;
                    var temperatureFahrenheit = CelsiusToFahrenheit(temperatureCelsius);
                    var tempAndHumidityText = "Temperature = " + temperatureCelsius.ToString("F1") + "C " + temperatureFahrenheit.ToString("F1") + "F " + "Relative Humidity = " + humidity.ToString("F1") + "%";
                    Debug.Print(tempAndHumidityText);

                    resultState = NrpeMessage.NrpeResultState.Ok;
                    statusString = tempAndHumidityText;
                    // Nagios Plugin Developers Guidelines:
                    // https://nagios-plugins.org/doc/guidelines.html#AEN200
                    // 'label'=value[UOM];[warn];[crit];[min];[max]
                    performanceData.Add("temp_fahrenheit", temperatureFahrenheit + ";" + CelsiusToFahrenheit(WarnCelsius) + ";" + CelsiusToFahrenheit(CriticalCelsius) + ";" + CelsiusToFahrenheit(MinCelsius) + ";" + CelsiusToFahrenheit(MaxCelsius));
                    performanceData.Add("temp_celsius", temperatureCelsius + ";" + WarnCelsius + ";" + CriticalCelsius + ";" + MinCelsius + ";" + MaxCelsius);
                    performanceData.Add("relative_humidity", humidity + "%" + ";" + WarnHumidity + ";" + CriticalHumidity + ";" + MinHumidity + ";" + MaxHumidity);
                }
                else
                {
                    // The sensor is erratic enough that we can't treat read failure as a critical error.
                    resultState = NrpeMessage.NrpeResultState.Warning;
                    statusString = "Could not read temperature.";
                }
            }

            return resultState;
        }

        /// <summary>
        /// When first starting up, we sometimes get a reading of all 0's. This is a kludge to get around it; it is probably possible
        /// to fix it properly at a lower level than here, but I haven't tried yet.
        /// </summary>
        /// <param name="RHT03"></param>
        private static void ReadUntilNonZeroReadingObtained(Dht22Sensor RHT03)
        {
            // Try a few times to get a non-zero reading
            const int maxReadAttempts = 2;
            int readAttempts = 0;
            bool gotNonZeroReading = false;
            
            while (readAttempts < maxReadAttempts && gotNonZeroReading == false)
            {
                RHT03.Read();
                // ReSharper disable CompareOfFloatsByEqualityOperator
                if (RHT03.Humidity != 0 || RHT03.Temperature != 0)
                // ReSharper enable CompareOfFloatsByEqualityOperator
                {
                    gotNonZeroReading = true;
                }
                readAttempts++;
                Thread.Sleep(2000);
            }
        }

        private double CelsiusToFahrenheit(double celsiusTemp)
        {
            return celsiusTemp * 1.8 + 32;
        }
    }

}



