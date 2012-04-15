using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace TicketSystem
{
    public class TicketReader
    {
        public TicketList writeFile(string file)
        {
            TextWriter tw = new StreamWriter(file, true);

            TicketList ticList = new TicketList();
            tw.Write(JsonConvert.SerializeObject(ticList, Formatting.Indented));
            tw.Close();

            return ticList;
        }

        public TicketList readFile(string file)
        {
            TextReader tr = new StreamReader(file);
            string raw = tr.ReadToEnd();
            tr.Close();
            TicketList ticList = JsonConvert.DeserializeObject<TicketList>(raw);
            return ticList;
        }
    }
}
