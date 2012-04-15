using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace TicketSystem
{
    public class TicketList
    {
        public List<StandardTicket> Tickets;

        public TicketList()
        {
            Tickets = new List<StandardTicket>();
        }

        public void AddItem(StandardTicket t)
        {
            Tickets.Add(t);
        }

        public void clearResponses(string name)
        {
            foreach (StandardTicket t in Tickets)
            {
                if (t.getName().ToLower() == name.ToLower())
                {
                    if (t.getResponse() != null)
                    {
                        Tickets.Remove(t);
                    }
                }
            }
        }

        public int responseCount(string name)
        {
            int i = 0;
            foreach (StandardTicket t in Tickets)
            {
                if (t.getName() == name)
                {
                    if (t.getResponse() != null)
                    {
                        i++;
                    }
                }
            }
            return i;
        }

        public int ticketCount(string name)
        {
            int i = 0;
            foreach (StandardTicket t in Tickets)
            {
                if (t.getName() == name)
                {
                    i++;
                }
            }
            return i;
        }
    }
}