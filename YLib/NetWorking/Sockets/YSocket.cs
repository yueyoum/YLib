using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

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

            public string ErrorReason = "";

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

            public void SocketConnect()
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
                }
                catch (Exception e)
                {
                    _sock = null;
                    ErrorReason = e.ToString();
                }
            }

            public void SocketClose()
            {
                SendQueue.Enqueue(null);
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
                    ErrorReason = e.ToString();
                    RecvQueue.Enqueue(null);
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
                        return null;
                    }

                    count += rec;
                }
                return data;
            }

            private void SocketRecv()
            {
                var lenBuf = RecvHandler(_headerLength);
                if (lenBuf == null)
                {
                    ErrorReason = "Recv len 0 bytes. Remote Close the connection";
                    RecvQueue.Enqueue(null);
                    return;
                }

                int len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBuf, 0));
                var data = RecvHandler(len);
                if (data == null)
                {
                    ErrorReason = "Recv data 0 bytes. Remote Close the connection";
                    RecvQueue.Enqueue(null);
                    return;
                }

                RecvQueue.Enqueue(data);
            }


            public void RunRecv()
            {
                if (!IsConnected)
                {
                    return;
                }

                while (!_die)
                {
                    if (_sock.Poll(2000, SelectMode.SelectRead))
                    {
                        if (_die)
                        {
                            break;
                        }
                        SocketRecv();
                    }
                }
            }

            public void RunSend()
            {
                if (!IsConnected)
                {
                    return;
                }

                while (!_die)
                {
                    var data = SendQueue.Dequeue();
                    if (_die)
                    {
                        break;
                    }

                    if (data == null)
                    {
                        break;
                    }

                    SocketSend(data);
                }
            }
        }


        private static class RecvQueue
        {
            private static Queue<byte[]> q = new Queue<byte[]>();
            public static int Count
            {
                get
                { 
                    lock (q)
                    {
                        return q.Count;
                    }
                }
            }

            public static byte[] Dequeue()
            {
                lock (q)
                {
                    while (q.Count == 0)
                    {
                        Monitor.Wait(q);
                    }
                    return q.Dequeue();
                }
            }

            public static void Enqueue(byte[] data)
            {
                lock (q)
                {
                    q.Enqueue(data);
                    Monitor.PulseAll(q);
                }
            }

            public static void Clear()
            {
                lock (q)
                {
                    q.Clear();
                }
            }
        }

        private static class SendQueue
        {
            private static Queue<byte[]> q = new Queue<byte[]>();
            public static int Count
            {
                get
                { 
                    lock (q)
                    {
                        return q.Count;
                    }
                }
            }

            public static byte[] Dequeue()
            {
                lock (q)
                {
                    while (q.Count == 0)
                    {
                        Monitor.Wait(q);
                    }
                    return q.Dequeue();
                }
            }

            public static void Enqueue(byte[] data)
            {
                lock (q)
                {
                    q.Enqueue(data);
                    Monitor.PulseAll(q);
                }
            }

            public static void Clear()
            {
                lock (q)
                {
                    q.Clear();
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




        public delegate void OnDataEventHandler(byte[] data);
        public static event OnDataEventHandler OnData = null;

        public delegate void OnConnectEvenetHandler();
        public static event OnConnectEvenetHandler OnConnect = null;

        public delegate void OnDisconnectEventHandler(string reason);
        public static event OnDisconnectEventHandler OnDisconnect = null;

        private static SocketWorker soWorker = null;


        private static string CloseSocketWorker()
        {
            string reason = "";
            if (soWorker != null)
            {
                reason = soWorker.ErrorReason;
                soWorker.SocketClose();
                soWorker = null;
            }
            return reason;
        }

        public static void Close()
        {
            var reason = CloseSocketWorker();
            if (OnDisconnect != null)
            {
                OnDisconnect(reason);
            }
        }

        public static void Start()
        {
            SendQueue.Clear();
            RecvQueue.Clear();

            if (soWorker != null)
            {
                CloseSocketWorker();
            }

            soWorker = new SocketWorker(Ip, Port, HeaderLength);
            soWorker.SocketConnect();
            if (!soWorker.IsConnected)
            {
                Close();
                return;
            }

            if (OnConnect != null)
            {
                OnConnect();
            }

            var _send = new Thread(soWorker.RunSend);
            var _recv = new Thread(soWorker.RunRecv);
            _send.Start();
            _recv.Start();
        }

        private static bool CheckRecvQueue()
        {
            var data = RecvQueue.Dequeue();
            if (data == null)
            {
                // closed!
                Close();
                return false;
            }

            if (OnData != null)
            {
                OnData(data);
            }

            return true;
        }

        // Loop For Normal Use
        public static void Loop()
        {
            while (CheckRecvQueue())
            {
            }
        }

        // Update For Unity3d Use
        public static void Update()
        {
            if (RecvQueue.Count > 0)
            {
                CheckRecvQueue();
            }
        }

        public static void Send(byte[] data)
        {
            if (soWorker == null)
            {
                Close();
                return;
            }
            SendQueue.Enqueue(data);
        }
    }
}

