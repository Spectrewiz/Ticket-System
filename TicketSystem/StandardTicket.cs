using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;
using Terraria;
using System.IO;
using Newtonsoft.Json;

namespace TicketSystem
{
    [Serializable]
    public class StandardTicket
    {
        public string Name;
        public string Ticket;
        public string Response;
        public string Time;
        public string Tag;

        public StandardTicket(string Name, string Ticket, string Response, string Time, string Tag)
        {
            this.Name = Name;
            this.Ticket = Ticket;
            this.Response = Response;
            this.Time = Time;
            this.Tag = Tag;
        }

        public string getName()
        {
            return Name;
        }

        public string getTicket()
        {
            return Ticket;
        }

        public string getResponse()
        {
            return Response;
        }

        public string getTime()
        {
            return Time;
        }

        public string getTag()
        {
            return Tag;
        }

        public void setTag(string tag)
        {
            Tag = tag;
        }

        public void setResponse(string response)
        {
            Response = response;
        }
    }
}