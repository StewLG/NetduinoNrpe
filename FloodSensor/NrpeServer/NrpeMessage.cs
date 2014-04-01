using System;
using System.Collections;
using Microsoft.SPOT;

namespace FloodSensor.NrpeServer
{
    public class NrpeMessage
    {
        public enum NrpePacketVersion : short
        {
            Version1 = 1,
            Version2 = 2,            
            Version3 = 3
        }

        public enum NrpePacketType : short
        {
            PacketQuery = 1,
            PacketResponse = 2
        }

        public enum NrpeResultState : short
        {
            Ok = 0,
            Warning = 1,
            Critical = 2,
            Unknown = 3
        }

        // [2 Byte int16_t] – Version number
        public Int16 VersionNumber;
        // [2 Byte int16_t] – Type (Query/Response)
        public Int16 Type;
        // [4 Byte u_int32_t] – CRC32 Checksum
        public UInt32 CrcChecksum;
        // [2 Byte int16_t] – result code (OK, WARNING, ERROR, UNKNOWN)
        public Int16 ResultCode;
        // [1024 Byte char] Buffer
        public const int BufferLength = 1024;
        public byte[] Buffer = new byte[BufferLength];
        public const int DummyBytesLength = 2;
        public byte[] DummyBytes = new byte[DummyBytesLength];

        private const int VersionNumberIndex = 0;
        private const int TypeIndex = 2;
        private const int CrcIndex = 4;
        private const int ResultIndex = 8;
        private const int BufferIndex = 10;
        private const int DummyBytesIndex = 1034;

        // All agree there are 2 bytes of padding at the end of the structure.
        public const int NrpeMessageLength = 1036;

        public string BufferAsString
        {
            get { return  new string(Tools.Bytes2Chars(Buffer)); }
            set { Buffer = Tools.Chars2Bytes(value.ToCharArray()); }
        }

        /// <summary>
        /// Does this appear to be a legal query packet?
        /// </summary>
        public bool IsLegalQuery 
        {
            get
            {
                bool versionNumberIsLegal = this.VersionNumber > 0 && this.VersionNumber <=  (short)NrpePacketVersion.Version3;
                bool packetTypeisLegal = this.Type == (short)NrpePacketType.PacketQuery;
                return versionNumberIsLegal && packetTypeisLegal;
            }
        }

        /// <summary>
        /// Construct from values
        /// </summary>
        public NrpeMessage(short versionNumber, short type, UInt32 crc, short resultCode, byte[] bufferMessage, byte[] dummyBytes)
        {
            this.VersionNumber = versionNumber;
            this.Type = type;
            this.CrcChecksum = crc;
            this.ResultCode = resultCode;
            this.Buffer = bufferMessage;
            this.DummyBytes = dummyBytes;
        }

        /// <summary>
        /// Construct from enums
        /// </summary>
        public NrpeMessage(NrpePacketVersion version, NrpePacketType type, NrpeResultState resultCode, string message, Hashtable performanceData)
        {
            VersionNumber = (short)version;
            Type = (short)type;
            ResultCode = (short)resultCode;
            BufferAsString = message + MakePerformanceDataSuffix(performanceData);
            // Calculate CRC
            CrcChecksum = ComputeCrcOfMessage();
        }

        private string MakePerformanceDataSuffix(Hashtable performanceData)
        {
            string performanceDataString = " | ";
            foreach (DictionaryEntry perfData in performanceData)
            {
                performanceDataString += perfData.Key + "=" + perfData.Value + ", ";
            }

            // Remove last ", "
            performanceDataString = performanceDataString.TrimEnd(new char[] { ' ', ',' });
            Debug.Print("Performance data string: " + performanceDataString);
            return performanceDataString;
        }


        /// <summary>
        /// Get an NRPE packet out as bytes suitable for transmission in a packet
        /// </summary>
        /// <returns></returns>
        public byte[] GetPacketBytes(out uint crc)
        {
            var packetBytes = new byte[NrpeMessageLength];

            // Note use of ByteReverser to handle endian issues
            InsertBytesIntoArray(packetBytes, VersionNumberIndex, Tools.ByteReverser(BitConverter.GetBytes(VersionNumber)));
            InsertBytesIntoArray(packetBytes, TypeIndex, Tools.ByteReverser(BitConverter.GetBytes(Type)));            
            // Set CRC to 0
            InsertBytesIntoArray(packetBytes, CrcIndex, BitConverter.GetBytes(0));
            InsertBytesIntoArray(packetBytes, ResultIndex, Tools.ByteReverser(BitConverter.GetBytes(ResultCode)));
            Array.Copy(Buffer, 0, packetBytes, BufferIndex, Buffer.Length);            
            InsertBytesIntoArray(packetBytes, DummyBytesIndex, DummyBytes);

            // Compute CRC            
            crc = ComputeCrcOfMessage(packetBytes);
            byte[] reversedCrcBytes = Tools.ByteReverser(BitConverter.GetBytes(crc));

            // Put CRC into place in the message
            InsertBytesIntoArray(packetBytes, CrcIndex, reversedCrcBytes);
            //Debug.Print("GetPacketBytes. " + packetBytes.Length  + " bytes. Computed CRC: " + crc.ToString("X"));

            // Return entire packet, ready for transmission
            return packetBytes;
        }

        private void InsertBytesIntoArray(byte[] byteArrayTarget, int indexInByteArrayTarget, byte[] incomingBytes)
        {
            Array.Copy(incomingBytes, 0, byteArrayTarget, indexInByteArrayTarget, incomingBytes.Length);
        }

        private uint ComputeCrcOfMessage()
        {
            uint crc;
            GetPacketBytes(out crc);
            return crc;
        }

        private uint ComputeCrcOfMessage(byte[] messageBytes)
        {
            //DebugPrintOutMessageInHex(messageBytes.Length + " Bytes in message to CRC, before CRC has been zeroed: ", messageBytes);
            var messageBytesScratchCopy = new byte[messageBytes.Length];
            Array.Copy(messageBytes, messageBytesScratchCopy, messageBytes.Length);

            // Note that we zero out any existing CRC
            ZeroOutChecksum(messageBytesScratchCopy);
            //DebugPrintOutMessageInHex(messageBytesScratchCopy.Length + " Bytes in message to CRC, after CRC has been zeroed:  ", messageBytesScratchCopy);

            var nrpeCrcMaker = new NrpeCrc();
            var nrpeCrc = nrpeCrcMaker.CalculateCrc32(messageBytesScratchCopy, messageBytesScratchCopy.Length);
            return nrpeCrc;
        }

        // Attempting hex crc calcuation to plug in here: http://www.lammertbies.nl/comm/info/crc-calculation.html

        /// <summary>
        /// Debug function to output the message as a hex string
        /// </summary>
        public static void DebugPrintOutMessageInHex(string messageLabel, byte[] messageBytes)
        {
            var hexString = Tools.BytesToHexString(messageBytes);
            Debug.Print(messageLabel + " raw bytes: " + hexString);
        }

        private static void ZeroOutChecksum(byte[] messageBytesToClear)
        {
            messageBytesToClear[CrcIndex] = 0;
            messageBytesToClear[CrcIndex + 1] = 0;
            messageBytesToClear[CrcIndex + 2] = 0;
            messageBytesToClear[CrcIndex + 3] = 0;
        }


        public void PrintDebug()
        {
            Debug.Print("NrpeMessage");
            Debug.Print("-----------");
            Debug.Print("Version Number: " + VersionNumber);
            Debug.Print("Type: " + Type);
            Debug.Print("CRC: " + CrcChecksum.ToString("X"));
            Debug.Print("Result Code: " + ResultCode);
            Debug.Print("Buffer: " + new string(Tools.Bytes2Chars(Buffer)));
            Debug.Print("Dummy Bytes: " + Tools.BytesToHexString(DummyBytes));
        }

    }
}
