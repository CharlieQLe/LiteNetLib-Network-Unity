using System;
using System.Collections.Generic;
using System.Net;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace LNLNetwork.Server {
    public delegate void HostDelegate();
    public delegate void CloseDelegate();
    public delegate bool ProcessConnectionDataDelegate(IPEndPoint remoteEndPoint, NetDataReader connectionData);
    public delegate void PeerConnectedDelegate(NetPeer peer);
    public delegate void PeerDisconnectedDelegate(NetPeer peer, DisconnectInfo disconnectInfo);
    public delegate bool FilterPeerDelegate(NetPeer peer);
    public delegate void WriteDataDelegate(NetDataWriter netDataWriter);
    public delegate void ReceiveDataDelegate(NetPeer peer, NetDataReader netDataReader);

    public static class NetServerManager {
        /// <summary>
        /// Invoked when the server tries to host.
        /// </summary>
        public static event HostDelegate HostEvent;

        /// <summary>
        /// Invoked when the server tries to close the connections.
        /// </summary>
        public static event CloseDelegate CloseEvent;

        /// <summary>
        /// Invoked when a client connects to the server.
        /// </summary>
        public static event PeerConnectedDelegate PeerConnectedEvent;

        /// <summary>
        /// Invoked when a client disconnects from the server.
        /// </summary>
        public static event PeerDisconnectedDelegate PeerDisconnectedEvent;

        private static ProcessConnectionDataDelegate processConnectionDataFunc;
        private static readonly Dictionary<byte, ReceiveDataDelegate> receiveDataEvents;
        private static readonly NetDataWriter netDataWriter;
        private static readonly EventBasedNetListener eventListener;
        private static readonly NetManager netManager;
        private static readonly List<NetPeer> connectedPeers;

        public static bool IsConnected => netManager.IsRunning;
        public static int ConnectedPeerCount => connectedPeers.Count;
        public static IReadOnlyList<NetPeer> ConnectedPeers => connectedPeers;

        static NetServerManager() {
            receiveDataEvents = new Dictionary<byte, ReceiveDataDelegate>();
            netDataWriter = new NetDataWriter();
            eventListener = new EventBasedNetListener();
            eventListener.ConnectionRequestEvent += OnConnectionRequest;
            eventListener.PeerConnectedEvent += OnPeerConnected;
            eventListener.PeerDisconnectedEvent += OnPeerDisconnected;
            eventListener.NetworkReceiveEvent += OnNetworkReceive;
            netManager = new NetManager(eventListener) {
                AutoRecycle = true
            };
            connectedPeers = new List<NetPeer>();
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
        /// Set the function to process the connection data.
        /// </summary>
        /// <param name="processDataFunc"></param>
        public static void SetProcessConnectionRequestFunc(ProcessConnectionDataDelegate processDataFunc) {
            processConnectionDataFunc = processDataFunc;
        }

        /// <summary>
        /// Attempt to host a server.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public static bool Host(ushort port) {
            if (netManager.Start(port)) {
                HostEvent?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempt to close the server.
        /// </summary>
        /// <param name="writeDataFunc"></param>
        /// <returns></returns>
        public static bool Close(WriteDataDelegate writeDataFunc) {
            if (!netManager.IsRunning) {
                return false;
            }
            netDataWriter.Reset();
            writeDataFunc?.Invoke(netDataWriter);
            netManager.DisconnectAll(netDataWriter.Data, 0, netDataWriter.Length);
            netManager.Stop();
            connectedPeers.Clear();
            CloseEvent?.Invoke();
            return true;
        }

        /// <summary>
        /// Attempt to disconnect a peer from the server.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="writeDataFunc"></param>
        public static void Disconnect(NetPeer peer, WriteDataDelegate writeDataFunc) {
            netDataWriter.Reset();
            writeDataFunc?.Invoke(netDataWriter);
            peer.Disconnect(netDataWriter);
        }

        /// <summary>
        /// Attempt to send data to the specified peer.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="messageId"></param>
        /// <param name="writeDataFunc"></param>
        /// <param name="deliveryMethod"></param>
        public static void SendTo(NetPeer peer, byte messageId, WriteDataDelegate writeDataFunc, DeliveryMethod deliveryMethod) {
            netDataWriter.Reset();
            netDataWriter.Put(messageId);
            writeDataFunc?.Invoke(netDataWriter);
            peer.Send(netDataWriter, deliveryMethod);
        }

        /// <summary>
        /// Attempt to send data to every peer.
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="writeDataFunc"></param>
        /// <param name="deliveryMethod"></param>
        public static void SendToAll(byte messageId, WriteDataDelegate writeDataFunc, DeliveryMethod deliveryMethod) {
            netDataWriter.Reset();
            netDataWriter.Put(messageId);
            writeDataFunc?.Invoke(netDataWriter);
            netManager.SendToAll(netDataWriter, deliveryMethod);
        }

        /// <summary>
        /// Attempt to send data to every filtered peer.
        /// </summary>
        /// <param name="filterPeerFunc"></param>
        /// <param name="messageId"></param>
        /// <param name="writeDataFunc"></param>
        /// <param name="deliveryMethod"></param>
        public static void SendToFilter(FilterPeerDelegate filterPeerFunc, byte messageId, WriteDataDelegate writeDataFunc, DeliveryMethod deliveryMethod) {
            netDataWriter.Reset();
            netDataWriter.Put(messageId);
            writeDataFunc?.Invoke(netDataWriter);
            for (int i = 0; i < connectedPeers.Count; i++) {
                NetPeer peer = connectedPeers[i];
                if (filterPeerFunc == null ? true : filterPeerFunc(peer)) {
                    connectedPeers[i].Send(netDataWriter, deliveryMethod);
                }
            }
        }

        private static void OnConnectionRequest(ConnectionRequest request) {
            if (processConnectionDataFunc == null || processConnectionDataFunc(request.RemoteEndPoint, request.Data)) {
                request.Accept();
            } else {
                request.Reject();
            }
        }

        private static void OnPeerConnected(NetPeer peer) {
            connectedPeers.Add(peer);
            PeerConnectedEvent?.Invoke(peer);
        }

        private static void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
            connectedPeers.Remove(peer);
            PeerDisconnectedEvent?.Invoke(peer, disconnectInfo);
        }

        private static void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) {
            while (reader.AvailableBytes > 0) {
                if (receiveDataEvents.TryGetValue(reader.GetByte(), out ReceiveDataDelegate receiveEvent)) {
                    receiveEvent?.Invoke(peer, reader);
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
            NetUtility.InjectSubsystems(typeof(NetServerManager), Tick);
            Application.quitting += () => {
                Close(_ => { });
                NetUtility.ResetSubsystems();
            };
        }
    }
}
