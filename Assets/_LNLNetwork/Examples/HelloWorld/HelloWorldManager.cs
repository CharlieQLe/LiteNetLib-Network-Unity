using LNLNetwork.Client;
using LNLNetwork.Server;
using UnityEngine;

namespace LNLNetwork.Examples.HelloWorld {
    public class HelloWorldManager : MonoBehaviour {
        private const byte ID_HELLO = 1;
        private const byte ID_WORLD = 2;

        private void Awake() {
            NetServerManager.RegisterMessage(ID_HELLO, (peer, _) => {
                Debug.Log("[Server] Hello");
                NetServerManager.SendTo(peer, ID_WORLD, _ => { }, LiteNetLib.DeliveryMethod.Unreliable);
            });
            NetServerManager.RegisterMessage(ID_WORLD, (peer, _) => {
                Debug.Log("[Server] World");
            });
            NetClientManager.RegisterMessage(ID_HELLO, (_) => {
                Debug.Log("[Client] Hello");
                NetClientManager.Send(ID_WORLD, _ => { }, LiteNetLib.DeliveryMethod.Unreliable);
            });
            NetClientManager.RegisterMessage(ID_WORLD, (_) => {
                Debug.Log("[Client] World");
            });

            NetServerManager.HostEvent += () => Debug.Log("[Server] Began hosting!");
            NetServerManager.CloseEvent += () => Debug.Log("[Server] Stopped server!");
            NetServerManager.PeerConnectedEvent += peer => Debug.Log($"[Server] Peer {peer.Id} connected!");
            NetServerManager.PeerDisconnectedEvent += (peer, disconnectInfo) => Debug.Log($"[Server] Peer {peer.Id} disconnected! Reason: {disconnectInfo.Reason}");

            NetClientManager.BeginConnectionEvent += () => Debug.Log("[Client] Attempting to connect to server...");
            NetClientManager.PeerConnectedEvent += () => Debug.Log("[Client] Connected to server!");
            NetClientManager.PeerDisconnectedEvent += disconnectInfo => Debug.Log($"[Client] Disconnected from server! Reason: {disconnectInfo.Reason}");
        }

        private void Start() {
            NetServerManager.Host(7777);
            NetClientManager.Connect("127.0.0.1", 7777, _ => { });
        }

        private void OnGUI() {
            if (GUI.Button(new Rect(16, 16, 128, 32), "Client Starts")) {
                Debug.Log("[Client] Hello");
                NetClientManager.Send(ID_WORLD, _ => { }, LiteNetLib.DeliveryMethod.Unreliable);
            } else if (GUI.Button(new Rect(16, 64, 128, 32), "Server Starts")) {
                Debug.Log("[Server] Hello");
                NetServerManager.SendToAll(ID_WORLD, _ => { }, LiteNetLib.DeliveryMethod.Unreliable);
            }
        }
    }
}