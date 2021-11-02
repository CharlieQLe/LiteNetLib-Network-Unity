using System;
using System.Collections.Generic;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace LNLNetwork {
    public static class NetUtility {
        public static void InjectSubsystems(Type systemType, Action updateEvent) {
            PlayerLoopSystem rootSystem = PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < rootSystem.subSystemList.Length; i++) {
                PlayerLoopSystem system = rootSystem.subSystemList[i];
                if (system.type == typeof(FixedUpdate)) {
                    List<PlayerLoopSystem> systems = new List<PlayerLoopSystem>(system.subSystemList);
                    systems.Insert(0, new PlayerLoopSystem() {
                        type = systemType,
                        updateDelegate = () => updateEvent()
                    });
                    system.subSystemList = systems.ToArray();
                    rootSystem.subSystemList[i] = system;
                    break;
                }
            }
            PlayerLoop.SetPlayerLoop(rootSystem);
        }

        public static void ResetSubsystems() => PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
    }
}
