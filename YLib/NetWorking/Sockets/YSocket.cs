using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace YLib.NetWorking.Sockets
{
    public static class YScoket
    {
        private class SocketWorker
        {
            private string _ip;
            private int _port;
            private int _headerLength;

            private Socket _sock = null;

            private bool _die = false;

            public bool IsConnected
            {
                get { return (_sock != null); }
            }

            public SocketWorker(string ip, int port, int headerLength)
            {
                _ip = ip;
                _port = port;
                _headerLength = headerLength;
            }

            private void SocketConnect()
            {
                if (IsConnected)
                {
                    return;
                }

                var endPoint = new IPEndPoint(IPAddress.Parse(_ip), _port);
                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    _sock.Connect(endPoint);
                    if (OnConnect != null)
                    {
                        OnConnect();
                    }
                }
                catch (Exception e)
                {
                    _sock = null;
                    Close(e.ToString());
                }
            }

            public void SocketClose()
            {
                _die = true;
                if (_sock != null)
                {
                    _sock.Close();
                    _sock = null;
                }
            }

            public void SocketSend(byte[] data)
            {
                var buffer = new byte[data.Length + _headerLength];
                var len = IPAddress.HostToNetworkOrder(data.Length);
                var lenBytes = BitConverter.GetBytes(len);

                lenBytes.CopyTo(buffer, 0);
                data.CopyTo(buffer, lenBytes.Length);

                try
                {
                    _sock.Send(buffer);
                }
                catch (Exception e)
                {
                    Close(e.ToString());
                }
            }

            private byte[] RecvHandler(int length)
            {
                var data = new byte[length];
                int count = 0;
                int rec = 0;
                while (count < length)
                {
                    rec = _sock.Receive(data, count, length - count, SocketFlags.None);

                    if (rec == 0)
                    {
                        return new byte[0];
                    }

                    count += rec;
                }

                return data;
            }

            private void SocketRecv()
            {
                var lenBuf = RecvHandler(_headerLength);
                if (lenBuf.Length == 0)
                {
                    Close("Recv len 0 bytes. Remote Close the connection");
                    return;
                }

                int len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBuf, 0));
                var data = RecvHandler(len);
                if (data.Length == 0)
                {
                    Close("Recv data 0 bytes. Remote Close the connection");
                    return;
                }


                if (OnData != null)
                {
                    OnData(data);
                }
            }


            public void Run()
            {
                if (!IsConnected)
                {
                    SocketConnect();
                }

                if (!IsConnected)
                {
                    return;
                }

                while (true)
                {
                    if (_die)
                    {
                        break;
                    }

                    if (_sock.Poll(100000, SelectMode.SelectRead))
                    {
                        SocketRecv();
                    }
                }

            }
        }

        private static int _headerLen = 4;

        public static string Ip { get; set;}
        public static int Port { get; set;}
        public static int HeaderLength
        {
            get { return _headerLen;}
            set { _headerLen = value;}
        }



        private delegate void DataToSendEventHandler(byte[] data);
        private static event DataToSendEventHandler DataToSend = null;

        public delegate void OnDataEventHandler(byte[] data);
        public static event OnDataEventHandler OnData = null;

        public delegate void OnConnectEvenetHandler();
        public static event OnConnectEvenetHandler OnConnect = null;

        public delegate void OnDisconnectEventHandler(string reason);
        public static event OnDisconnectEventHandler OnDisconnect = null;

        private static SocketWorker soWorker = null;


        private static void CloseSocketWorker()
        {
            DataToSend = null;
            if (soWorker != null)
            {
                soWorker.SocketClose();
                soWorker = null;
            }
        }

        public static void Close()
        {
            CloseSocketWorker();
            if (OnDisconnect != null)
            {
                OnDisconnect("");
            }
        }

        public static void Close(string reason)
        {
            CloseSocketWorker();
            if (OnDisconnect != null)
            {
                OnDisconnect(reason);
            }
        }


        public static void Start()
        {
            if (soWorker != null)
            {
                CloseSocketWorker();
            }

            soWorker = new SocketWorker(Ip, Port, HeaderLength);
            DataToSend += soWorker.SocketSend;

            var th = new Thread(soWorker.Run);
            th.Start();
        }

        public static void Send(byte[] data)
        {
            if (DataToSend == null)
            {
                Close();
                return;
            }
            DataToSend(data);
        }

    }
}

