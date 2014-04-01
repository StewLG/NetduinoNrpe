using System;

namespace FloodSensor
{
    /// <summary>
    /// Ported from https://github.com/KristianLyng/nrpe/blob/master/src/utils.c
    /// I am not sure if this was strictly necessary, but then I could not seem to get Utility.ComputeCRC 
    /// (http://msdn.microsoft.com/query/dev11.query?appId=Dev11IDEF1&l=EN-US&k=k%28Microsoft.SPOT.Hardware.Utility.ComputeCRC%29;k%28TargetFrameworkMoniker-.NETMicroFramework)
    /// to return the same result as this function, no matter what seed I tried with it.
    /// </summary>
    class NrpeCrc
    {
        private const int CrcTableLength = 256;
        static private readonly UInt32[] Crc32Table = new UInt32[CrcTableLength];

        public NrpeCrc()
        {
            generateCrc32Table();
        }

        // Build the crc table - must be called before calculating the crc value 
        private void generateCrc32Table()
        {
            const uint poly = 0xEDB88320;
            for (int i = 0; i < 256; i++)
            {
                var crc = (UInt32)i;
                for (int j = 8; j > 0; j--)
                {
                    if ((crc & (UInt32)1) > 0)
                    {
                        crc = (crc >> 1) ^ poly;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
                Crc32Table[i] = crc;
            }
        }

        /// <summary>
        /// Calculates the CRC 32 value for a buffer
        /// </summary>
        public UInt32 CalculateCrc32(byte[] buffer, int bufferSize)
        {
            int currentIndex;
            uint crc = 0xFFFFFFFF;

            for (currentIndex = 0; currentIndex < bufferSize; currentIndex++)
            {
                int thisChar = buffer[currentIndex];
                crc = ((crc >> 8) & 0x00FFFFFF) ^ Crc32Table[(crc ^ thisChar) & 0xFF];
            }

            return (crc ^ 0xFFFFFFFF);
        }
    }
}



