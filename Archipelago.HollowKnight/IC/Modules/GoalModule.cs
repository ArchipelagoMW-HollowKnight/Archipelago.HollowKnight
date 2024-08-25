using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Packets;
using ItemChanger;
using ItemChanger.Modules;
using System;
using System.Threading.Tasks;

namespace Archipelago.HollowKnight.IC.Modules
{
    public class GoalModule : Module
    {
        private ArchipelagoSession session => ArchipelagoMod.Instance.session;

        private Goal goal;

        public override void Initialize()
        {
            goal = Goal.GetGoal(ArchipelagoMod.Instance.SlotData.Options.Goal);
            goal.Select();
        }

        public override void Unload()
        {
            goal.Deselect();
            goal = null;
        }

        public async Task DeclareVictoryAsync()
        {
            try
            {
                await session.Socket.SendPacketAsync(new StatusUpdatePacket()
                {
                    Status = ArchipelagoClientState.ClientGoal
                }).TimeoutAfter(1000);
            }
            catch (Exception ex) when (ex is TimeoutException or ArchipelagoSocketClosedException)
            {
                ItemChangerMod.Modules.Get<ItemNetworkingModule>().ReportDisconnect();
            }
        }
    }
}
