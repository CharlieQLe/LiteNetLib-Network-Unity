using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace LNLNetwork.Client {
    public delegate void BeginConnectionDelegate();
    public delegate void PeerConnectedDelegate();
    public delegate void PeerDisconnectedDelegate(DisconnectInfo disconnectInfo);
    public delegate void WriteDataDelegate(NetDataWriter netDataWriter);
    public delegate void ReceiveDataDelegate(NetDataReader netDataReader);

    public static class NetClientManager {
        /// <summary>
        /// Invoked when the client attempts to connect.
        /// </summary>
        public static event BeginConnectionDelegate BeginConnectionEvent;

        /// <summary>
        /// Invoked when the client connects to the server.
        /// </summary>
        public static event PeerConnectedDelegate PeerConnectedEvent;

        /// <summary>
        /// Invoked when the client disconnects from the server.
        /// </summary>
        public static event PeerDisconnectedDelegate PeerDisconnectedEvent;

        private static readonly Dictionary<byte, ReceiveDataDelegate> receiveDataEvents;
        private static readonly NetDataWriter netDataWriter;
        private static readonly EventBasedNetListener eventListener;
        private static readonly NetManager netManager;
        private static NetPeer serverPeer;

        /// <summary>
        /// Get the connection state.
        /// </summary>
        public static ConnectionState ConnectionState => serverPeer == null ? ConnectionState.Disconnected : serverPeer.ConnectionState;

        /// <summary>
        /// Get the ping to the server in milliseconds.
        /// </summary>
        public static int Ping => serverPeer == null ? -1 : serverPeer.Ping;

        static NetClientManager() {
            receiveDataEvents = new Dictionary<byte, ReceiveDataDelegate>();
            netDataWriter = new NetDataWriter();
            eventListener = new EventBasedNetListener();
            eventListener.PeerConnectedEvent += OnPeerConnected;
            eventListener.PeerDisconnectedEvent += OnPeerDisconnected;
            eventListener.NetworkReceiveEvent += OnNetworkReceive;
            netManager = new NetManager(eventListener) {
                AutoRecycle = true
            };
        }

        /// <summary>
        /// Register a message receive event.
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="receiveDataEvent"></param>
        public static void RegisterMessage(byte messageId, ReceiveDataDelegate receiveDataEvent) => receiveDataEvents[messageId] = receiveDataEvent;

        /// <summary>
        /// Remove a message receive event.
        /// </summary>
        /// <param name="messageId"></param>
        public static void UnregisterMessage(byte messageId) => receiveDataEvents.Remove(messageId);

        /// <summary>
        /// Attempt to connect to the server at the specified ip and port.
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="writeDataFunc"></param>
        /// <returns></returns>
        public static bool Connect(string ip, ushort port, WriteDataDelegate writeDataFunc) {
            if (netManager.Start()) {
                netDataWriter.Reset();
                writeDataFunc?.Invoke(netDataWriter);
                NetPeer peer = netManager.Connect(ip, port, netDataWriter);
                if (peer == null || peer == serverPeer) {
                    return false;
                }
                serverPeer = peer;
                BeginConnectionEvent?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempt to disconnect from the server.
        /// </summary>
        /// <param name="writeDataFunc"></param>
        public static void Disconnect(WriteDataDelegate writeDataFunc) {
            netDataWriter.Reset();
            writeDataFunc?.Invoke(netDataWriter);
            netManager.DisconnectPeer(serverPeer, netDataWriter);
        }

        /// <summary>
        /// Attempt to send data to the server.
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="writeDataFunc"></param>
        /// <param name="deliveryMethod"></param>
        public static void Send(byte messageId, WriteDataDelegate writeDataFunc, DeliveryMethod deliveryMethod) {
            if (serverPeer == null) {
                return;
            }
            netDataWriter.Reset();
            netDataWriter.Put(messageId);
            writeDataFunc?.Invoke(netDataWriter);
            serverPeer.Send(netDataWriter, deliveryMethod);
        }

        private static void OnPeerConnected(NetPeer peer) {
            serverPeer = peer;
            PeerConnectedEvent?.Invoke();
        }

        private static void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
            serverPeer = null;
            netManager.Stop();
            PeerDisconnectedEvent?.Invoke(disconnectInfo);
        }
    
        private static void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) {
            while (reader.AvailableBytes > 0) {
                if (receiveDataEvents.TryGetValue(reader.GetByte(), out ReceiveDataDelegate receiveEvent)) {
                    receiveEvent?.Invoke(reader);
                }
            }
        }

        private static void Tick() {
            if (netManager.IsRunning) {
                netManager.PollEvents();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize() {
            NetUtility.InjectSubsystems(typeof(NetClientManager), Tick);
            Application.quitting += () => {
                Disconnect(_ => { });
                NetUtility.ResetSubsystems();
            };
        }
    }
}
