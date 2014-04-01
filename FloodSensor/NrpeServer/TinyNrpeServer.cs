using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using FloodSensor.NrpeCheckers;
using FloodSensor.NrpeCheckers.Temperature;
using Microsoft.SPOT;

namespace FloodSensor.NrpeServer
{
    public class TinyNrpeServer : IDisposable
    {
        private bool _disposed;

        public static bool StopNrpeServer = false;
        public AutoResetEvent TinyNrpeServerIsShuttingDown = new AutoResetEvent(false);
        public const int DefaultNrpePort = 5666;
        private const int PollTimeout = 1000;
        /// <summary>
        /// Did the server stop normally? (Or was there an exception?)
        /// </summary>
        public bool NrpeServerStoppedNormally = true;

        /// <summary>
        /// How many connections we'll queue up, evidently
        /// </summary>
        const int MaxLengthOfPendingConnectionsQueue = 1;

        /// <summary>
        /// Contains a reference to the socket
        /// </summary>
        private Socket _socket;
        /// <summary>
        /// NetworkStream rides on top of the socket. This provides us with the logical bytes, without the underlying packets.
        /// </summary>
        private NetworkStream _networkStream;
        /// <summary>
        /// Stores the TCP port connected to
        /// </summary>
        private readonly ushort _portNumber;
        /// <summary>
        /// Stores the hostname connected to
        /// </summary>
        private string _hostname;

        private Socket _listenerSocket;


        public TinyNrpeServer(ushort portNumber = DefaultNrpePort)
        {
            _portNumber = portNumber;
        }


        /// <summary>
        /// Deletes an instance of the <see cref="TinyNrpeServer"/> class.
        /// </summary>
        ~TinyNrpeServer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases resources used by this <see cref="TinyNrpeServer"/> object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the resources associated with the <see cref="DhtSensor"/> object.
        /// </summary>
        /// <param name="disposing">
        /// <b>true</b> to release both managed and unmanaged resources;
        /// <b>false</b> to release only unmanaged resources.
        /// </param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        protected void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                try
                {
                    this.CloseConnection();
                    this.CloseListeningSocket();

                    // Although it works, recovery after an exception in the TinyNrpeServer is currently a bit ragged.
                    // Note that actually *accomplishing* the Closing of the Listening Socket takes a minute or two, during which time we will repeatedly
                    // re-enter the RunServer(), and immediate get a SocketException in StartListening(). During this time we are waiting
                    // for the Socket to be released, and until it has we can't finish StartListening() without an exception. If there is a less
                    // brutal way to accomplish this I'd love to know about it.
                }
                catch (Exception e)
                {
                    Debug.Print("Caught exception in TinyNrpeServer.Dispose(): " + e.Message);
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        static readonly object RunServerLock = new object();

        /// <summary>
        /// Run the server
        /// </summary>
        public void RunServer()
        {
            EnsureNotDisposed();

            lock (RunServerLock)
            {
                Debug.Print("Starting NRPE Server");
                WatchpointDebugInfo();

                using (var nrpeServer = new TinyNrpeServer())
                {
                    NrpeServerStoppedNormally = true;
                    try
                    {
                        nrpeServer.StartListening();
                        WatchpointDebugInfo();

                        while (StopNrpeServer == false)
                        {
                            // Accept incoming connections
                            bool gotConnection = nrpeServer.AcceptConnection();
                            if (gotConnection)
                            {
                                WatchpointDebugInfo();

                                // Check in with Watchdog
                                Program.InactivityWatchdog.CheckIn();

                                // Flash the LED to indicate message processing activity
                                LedFlasher.FlashLed(1);

                                // Process each request as it arrives
                                NrpeMessage nrpeMessageQuery = nrpeServer.ReceiveNrpeMessage();

                                if (nrpeMessageQuery.IsLegalQuery)
                                {
                                    NrpeMessage nrpeMessageReply = NrpeChecker.ProcessQuery(nrpeMessageQuery);
                                    nrpeServer.SendNrpeMessage(nrpeMessageReply);
                                }
                                else
                                {
                                    // There's not enough room in the Netduino for SSL, so this isn't possible:
                                    // http://forums.netduino.com/index.php?/topic/2302-netduino-ssltls/
                                    Debug.Print("Skipping unreadable incoming message (is SSL disabled for your check_nrpe request? This client can't handle SSL messages.");
                                }

                                nrpeServer.CloseConnection();
                                WatchpointDebugInfo();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Print("Exception in NRPE server: " + e.Message);
                        if (e.GetType() == typeof (SocketException))
                        {
                            var socketException = (SocketException) e;
                            Debug.Print("Socket exception in NRPE Server. Error Code: " + socketException.ErrorCode);
                        }
                        nrpeServer.Dispose();
                        NrpeServerStoppedNormally = false;
                        // We have to suppress the exception
                        // throw;
                    }
                }
            }

            WatchpointDebugInfo();
            Debug.Print("Ending NRPE Server.");

            // Indicate that the NRPE server thread is exiting, whether failing or not.
            TinyNrpeServerIsShuttingDown.Set();                
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException();
            }
        }

        private static void WatchpointDebugInfo()
        {
            var freeMemory = Debug.GC(false);
            Debug.Print("Free memory: " + freeMemory);
        }

        private void StartListening()
        {
            _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, this._portNumber));
            _listenerSocket.Listen(MaxLengthOfPendingConnectionsQueue);
            Debug.Print("Listening on port " + _portNumber);
        }

        private bool AcceptConnection()
        {
            Debug.Print("Waiting for connection..");

            // http://forums.netduino.com/index.php?/topic/10299-clean-way-to-interrupt-socketaccept/

            // Poll, waiting for bytes to appear, and checking for the stop signal
            while (!this._listenerSocket.Poll(PollTimeout, SelectMode.SelectRead))
            {
                if (StopNrpeServer)
                {
                    return false;
                }
            }

            // Bytes have appeared, accept the connection
            this._socket = this._listenerSocket.Accept();
            this._hostname = ((IPEndPoint)this._socket.RemoteEndPoint).Address.ToString();
            Debug.Print("Accepted connection from " + this._hostname);
            _networkStream = new NetworkStream(this._socket);
            return true;
        }

        private void CloseConnection()
        {
            Debug.Print("Closing NetworkStream...");
            if (_networkStream != null)
            {
                _networkStream.Dispose();
            }
            else
            {
                Debug.Print("Null _networkStream, skipping dispose...");
            }
        }

        private void CloseListeningSocket()
        {
            if (_listenerSocket != null)
            {
                try
                {                    
                    _listenerSocket.Close();
                }
                catch (Exception e)
                {
                    Debug.Print("Suppressed exception during listenerSocket.Close(): " + e.Message);
                }
            }
            else
            {
                Debug.Print("Null _listenerSocket, skipping close...");
            }
        }

        public NrpeMessage ReceiveNrpeMessage()
        {
            EnsureNotDisposed();

            Int16 packetVersion = Tools.ReadShort(this._networkStream);
            Int16 packetType = Tools.ReadShort(this._networkStream);
            UInt32 packetCrc = Tools.ReadUInt(this._networkStream);
            Int16 packetResultCode = Tools.ReadShort(this._networkStream);            
            int bufferBytesRead;
            var bufferBytes = ReceiveBinary(NrpeMessage.BufferLength, out bufferBytesRead);
            int dummyBytesRead;
            var dummyBytes = ReceiveBinary(NrpeMessage.DummyBytesLength, out dummyBytesRead);

            return new NrpeMessage(packetVersion, packetType, packetCrc, packetResultCode, bufferBytes, dummyBytes);
        }

        /// <summary>
        /// Receives binary data from the socket 
        /// </summary>
        /// <param name="length">The amount of bytes to receive</param>
        /// <param name="bytesRead">Count of bytes actually read</param>
        /// <returns>The binary data</returns>
        private byte[] ReceiveBinary(int length, out int bytesRead)
        {
            var binaryBytes = new byte[length];
            bytesRead = this._networkStream.Read(binaryBytes, 0, length);
            
            return binaryBytes;
        }

        private void SendNrpeMessage(NrpeMessage nrpeMessageReply)
        {            
            Debug.Print("About to send reply message:");
            nrpeMessageReply.PrintDebug();
            uint crc;
            var packetBytes = nrpeMessageReply.GetPacketBytes(out crc);
            SendBinary(packetBytes);
            Debug.Print("Reply message sent");
        }

        /// <summary>
        /// Sends binary data on the socket
        /// </summary>
        /// <param name="binaryBytes"></param>
        private void SendBinary(byte[] binaryBytes)
        {
            Debug.Print("Writing " + binaryBytes.Length + " bytes to network stream");
            NrpeMessage.DebugPrintOutMessageInHex(binaryBytes.Length + " Bytes sent over the wire: ", binaryBytes);
            this._networkStream.Write(binaryBytes, 0, binaryBytes.Length);
        }




    }
}


