using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;

namespace TicketPlugin
{
    public class Player
    {
        public int Index { get; set; }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
        //Add other variables here - MAKE SURE YOU DON'T MAKE THEM STATIC

        public Player(int index)
        {
            Index = index;
        }

        public static Player GetPlayerByName(string name)
        {
            var player = TShock.Utils.FindPlayer(name)[0];
            if (player != null)
            {
                foreach (Player ply in TicketPlugin.Players)
                {
                    if (ply.TSPlayer == player)
                    {
                        return ply;
                    }
                }
            }
            return null;
        }
        
        protected CanSubmitTickets TicState = CanSubmitTickets.yes;
        public void SetTicState(CanSubmitTickets ticstate)
        {
            TicState = ticstate;
        }
        public CanSubmitTickets GetTicState()
        {
            return TicState;
        }
        public enum CanSubmitTickets
        {
            yes,
            no
            //maybe so? :P
        }
    }
}
