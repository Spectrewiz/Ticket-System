using System;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using System.IO;
using Newtonsoft.Json;
using System.Net;

namespace TicketSystem
{
    [APIVersion(1, 12)]
    public class TicketSystem : TerrariaPlugin
    {
        #region Main Plugin
        public static List<Player> Players = new List<Player>();
        public static string save = "";
        public static string banned = "";
        public static TicketList ticketlist;
        public static int update = 0;
        public static string downloadFromUpdate;
        public static string versionFromUpdate;
        public static DateTime lastupdate = DateTime.Now;
        public static string tagpath = Path.Combine(TShock.SavePath, @"TicketSystem\Tags.txt");
        public static Color bluebase = new Color(30, 144, 255);
        public static Color bluesecondarybase = new Color(135, 206, 255);
        public static Config Config { get; set; }
        internal static string ConfigPath { get { return Path.Combine(TShock.SavePath, @"TicketSystem\Config.json"); } }
        public override string Name
        {
            get { return "Ticket System"; }
        }

        public override string Author
        {
            get { return "Spectrewiz"; }
        }

        public override string Description
        {
            get { return "This plugin allows users in game to file tickets (complaints) that admins can access."; }
        }

        public override Version Version
        {
            get { return new Version(1, 2, 11); }
        }

        public override void Initialize()
        {
            GameHooks.Initialize += OnInitialize;
            NetHooks.GreetPlayer += OnGreetPlayer;
            ServerHooks.Leave += OnLeave;
            GameHooks.Update += OnUpdate;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Initialize -= OnInitialize;
                NetHooks.GreetPlayer -= OnGreetPlayer;
                ServerHooks.Leave -= OnLeave;
                GameHooks.Update -= OnUpdate;
            }
            base.Dispose(disposing);
        }

        public TicketSystem(Main game)
            : base(game)
        {
            Order = 10;
        }

        public void OnInitialize()
        {
            Commands.ChatCommands.Add(new Command(Ticket, "ticket", "hlpme"));
            Commands.ChatCommands.Add(new Command("TicketList", TicketListCommmand, "ticketlist", "ticlist"));
            Commands.ChatCommands.Add(new Command("TicketClear", TicketClear, "ticketclear", "ticketsclear", "ticclear", "ticsclear"));
            Commands.ChatCommands.Add(new Command("TicketBan", TicBan, "ticketban", "ticban"));
            Commands.ChatCommands.Add(new Command("TicketReload", Reload, "ticketreload", "ticreload"));

            save = Path.Combine(TShock.SavePath, @"TicketSystem\Tickets.json");
            banned = Path.Combine(TShock.SavePath, @"TicketSystem\Banned.txt");
            TicketReader Reader = new TicketReader();
            Config = new Config();

            if (File.Exists(save))
            {
                try
                {
                    ticketlist = Reader.readFile(save);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    if (ticketlist.Tickets.Count != 0)
                        Console.WriteLine(ticketlist.Tickets.Count + " tickets have been loaded.");
                    else
                        Console.WriteLine("There are no tickets.");
                    Console.ResetColor();
                    try
                    {
                        if (File.Exists(ConfigPath))
                            Config = Config.Read(ConfigPath);
                        Config.Write(ConfigPath);
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error in TicketSystem Config.json file! Check log for more details.");
                        Console.WriteLine(e.Message);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Log.Error("Config Exception");
                        Log.Error(e.ToString());
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error in Tickets.json file! Check log for more details.");
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                    Log.Error("------------- Config Exception in Ticket List file (Tickets.json) -------------");
                    Log.Error(e.Message);
                    Log.Error("---------------------------------- Error End ----------------------------------");
                    Console.ForegroundColor = ConsoleColor.Red;
                }
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(TShock.SavePath, "TicketSystem"));
                ticketlist = Reader.writeFile(save);
                using (StreamWriter writer = new StreamWriter(tagpath, true))
                {
                    writer.WriteLine("Default");
                    writer.WriteLine("Grief");
                    writer.WriteLine("Report");
                    writer.WriteLine("High-Importance");
                }
                using (StreamWriter writer = new StreamWriter(Path.Combine(TShock.SavePath, @"TicketSystem\loginmsg.txt"), true))
                {
                    writer.WriteLine("To write a complaint, use \"/ticket [-t:<tag>] <Message>\"");
                    writer.WriteLine("NOTE: Tags are optional. To view a list of tags, use \"/ticket tags\"");
                }
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("There are no tickets.");
                Console.ResetColor();
                Log.Info("No tickets submitted.");
            }
        }

        public void OnUpdate()
        {
            if (update == 0)
            {
                if (UpdateChecker())
                    update++;
                else
                    update--;
            }
            else if (update < 0)
            {
                if ((DateTime.Now - lastupdate).TotalHours >= 3)
                {
                    if (UpdateChecker())
                        update = 1;
                    else
                        lastupdate = DateTime.Now;
                }
            }
        }

        public void OnGreetPlayer(int who, HandledEventArgs e)
        {
            lock (Players)
                Players.Add(new Player(who));
            string name = TShock.Players[who].Name.ToLower();
            string line;
            bool seenResponses = false;
            var ListedPlayer = Player.GetPlayerByName(name);
            int count = ticketlist.Tickets.Count;
            if (File.Exists(save))
            {
                int i = ticketlist.responseCount(TShock.Players[who].Name);
                int x = ticketlist.ticketCount(TShock.Players[who].Name);
                if (i >= 1)
                {
                    TShock.Players[who].SendMessage("--- Responses for Tickets (" + i + "/" + x + ") ---", bluebase);
                    lock (ticketlist.Tickets)
                    {
                        foreach (StandardTicket t in ticketlist.Tickets)
                        {
                            if (t.getName() == TShock.Players[who].Name)
                            {
                                if (t.getResponse() != null)
                                {
                                    TShock.Players[who].SendMessage("[" + t.getTag().Trim() + "] " + t.getTicket().Trim() + " | " + t.getResponse(), bluesecondarybase);
                                }
                            }
                        }
                    }
                    TShock.Players[who].SendMessage("To see the responses again, type /ticket responses", bluebase);
                    TShock.Players[who].SendMessage("To clear the responses, type /ticket clearresponses", bluebase);
                }
                seenResponses = true;
            }
            if (File.Exists(banned))
            {
                using (StreamReader reader = new StreamReader(banned))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (String.Compare(line, name) == 0)
                        {
                            ListedPlayer.SetTicState(Player.CanSubmitTickets.no);
                        }
                    }
                }
            }
            if (!TShock.Players[who].Group.HasPermission("TicketList") && (ListedPlayer.GetTicState() != Player.CanSubmitTickets.no) && seenResponses)
            {
                if (!File.Exists(Path.Combine(TShock.SavePath, @"TicketSystem\loginmsg.txt")))
                    TShock.Players[who].SendMessage("To write a complaint, use /ticket <Message>", bluebase);
                else
                {
                    using (StreamReader reader = new StreamReader(Path.Combine(TShock.SavePath, @"TicketSystem\loginmsg.txt")))
                    {
                        while (reader.Peek() >= 0)
                        {
                            TShock.Players[who].SendMessage(reader.ReadLine(), bluebase); 
                        }
                    }
                }
            }
            else if ((TShock.Players[who].Group.HasPermission("TicketList")) && (count != 0))
                TShock.Players[who].SendMessage("There are " + count + " tickets submitted, use /ticketlist to view them.", bluebase);
            if (TShock.Players[who].Group.Name.ToLower() == "superadmin")
                if (update > 0)
                {
                    TShock.Players[who].SendMessage("Update for Ticket System available! Check log for download link.", Color.Yellow);
                    Log.Info(string.Format("NEW VERSION: {0}  |  Download here: {1}", versionFromUpdate, downloadFromUpdate));
                }
        }

        public void OnLeave(int ply)
        {
            lock (Players)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].Index == ply)
                    {
                        Players.RemoveAt(i);
                        break;
                    }
                }
            }
        }
        #endregion

        #region Commands and whatnot
        public static void Ticket(CommandArgs args)
        {
            var FindMe = Player.GetPlayerByName(args.Player.Name);
            if (FindMe.GetTicState() == Player.CanSubmitTickets.no)
            {
                args.Player.SendMessage("You cannot send tickets because you have had that privilege revoked.", Color.Red);
            }
            else if ((FindMe.GetTicState() == Player.CanSubmitTickets.yes) && (args.Parameters.Count < 1))
            {
                args.Player.SendMessage("You must enter a message!", Color.Red);
            }
            else if (args.Parameters[0].ToLower() == "clearresponses")
            {
                try
                {
                    lock (ticketlist.Tickets)
                    {
                        for (int i = ticketlist.Tickets.Count - 1; i >= 0; i++)
                        {
                            if (ticketlist.Tickets[i].getName() == args.Player.Name)
                            {
                                if (ticketlist.Tickets[i].getResponse() != null)
                                    ticketlist.Tickets.Remove(ticketlist.Tickets[i]);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
                UpdateTicketsInJSON(save);
                args.Player.SendMessage("Your responses have been cleared!", bluebase);
            }
            else if (args.Parameters[0].ToLower() == "responses")
            {
                if (File.Exists(save))
                {
                    int i = ticketlist.responseCount(args.Player.Name);
                    int x = ticketlist.ticketCount(args.Player.Name);
                    if (i != 0)
                    {
                        args.Player.SendMessage("--- Responses for Tickets (" + i + "/" + x + ") ---", bluebase);
                        lock (ticketlist.Tickets)
                        {
                            foreach (StandardTicket t in ticketlist.Tickets)
                            {
                                if (t.getName() == args.Player.Name)
                                {
                                    if (t.getResponse() != null)
                                    {
                                        args.Player.SendMessage("(" + t.getTicket().Trim() + "): " + t.getResponse(), bluesecondarybase);
                                    }
                                }
                            }
                        }
                        args.Player.SendMessage("To clear the responses, type /ticket clearresponses", bluebase);
                    }
                }
            }
            else if ((args.Parameters[0].ToLower() == "tags") || (args.Parameters[0].ToLower() == "tag") || (args.Parameters[0].ToLower() == "taglist"))
            {
                string[] officialtags = File.ReadAllText(tagpath).Split('\n');
                args.Player.SendMessage("Tags:", bluebase);
                foreach (string officialtag in officialtags)
                {
                    if (officialtag.StartsWith(" ") || officialtag == null || officialtag == "")
                        continue;
                    args.Player.SendMessage(officialtag, bluesecondarybase);
                }
            }
            else if (args.Parameters[0].ToLower().StartsWith("-tag:") || args.Parameters[0].ToLower().StartsWith("-t:"))
            {
                string tag = args.Parameters[0].ToLower().Split(':')[1];
                string[] officialtags = File.ReadAllText(tagpath).Split('\n');
                for (int i = 0; i < officialtags.Length; i++)
                {
                    officialtags[i] = officialtags[i].Trim().ToLower();
                }
                if (!((IList<string>)officialtags).Contains(tag.Trim()))
                    tag = officialtags[0];
                if ((tag == "") || (tag == null))
                {
                    if (!((IList<string>)officialtags).Contains(args.Parameters[1].ToLower().Trim()))
                        tag = officialtags[0];
                }
                try
                {
                    string text = "";
                    foreach (string word in args.Parameters)
                    {
                        if (word == args.Parameters[0])
                            continue;
                        else
                            text = text + word + " ";
                    }

                    ticketlist.AddItem(new StandardTicket(args.Player.Name, text, null, DateTime.Now.ToString(), tag));

                    UpdateTicketsInJSON(save);

                    args.Player.SendMessage("Your Ticket has been sent!", bluebase);
                    args.Player.SendMessage("Note: It has been tagged as " + tag + ". Use \"/ticket tags\" to view a list of valid tags.", bluesecondarybase);
                    foreach (Player player in TicketSystem.Players)
                    {
                        if (player.TSPlayer.Group.HasPermission("TicketList"))
                        {
                            player.TSPlayer.SendMessage(string.Format("{0} just submitted a ticket: {1}", args.Player.Name, text), bluebase);
                        }
                    }
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write(args.Player.Name + " just submitted a ticket: ", bluebase);
                    Console.ResetColor();
                    Console.WriteLine(text);
                    if (Config.beepOnTicketSubmission)
                        Console.Beep(637, 100);
                }
                catch (Exception e)
                {
                    args.Player.SendMessage("Your ticket could not be sent, contact an administrator.", Color.Red);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                    Log.Error(e.Message);
                }
            }
            else
            {
                try
                {
                    string[] officialtags = File.ReadAllText(tagpath).Split('\n');
                    for (int i = 0; i < officialtags.Length; i++)
                    {
                        officialtags[i] = officialtags[i].Trim().ToLower();
                    }
                    string text = "";
                    foreach (string word in args.Parameters)
                    {
                        text = text + word + " ";
                    }

                    ticketlist.AddItem(new StandardTicket(args.Player.Name, text, null, DateTime.Now.ToString(), officialtags[0]));

                    UpdateTicketsInJSON(save);

                    args.Player.SendMessage("Your Ticket has been sent!", bluebase);
                    foreach (Player player in TicketSystem.Players)
                    {
                        if (player.TSPlayer.Group.HasPermission("TicketList"))
                        {
                            player.TSPlayer.SendMessage(string.Format("{0} just submitted a ticket: {1}", args.Player.Name, text), bluebase);
                        }
                    }
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write(args.Player.Name + " just submitted a ticket: ", bluebase);
                    Console.ResetColor();
                    Console.WriteLine(text);
                    if (Config.beepOnTicketSubmission)
                        Console.Beep(637, 100);
                }
                catch (Exception e)
                {
                    args.Player.SendMessage("Your ticket could not be sent, contact an administrator.", Color.Red);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                    Log.Error(e.Message);
                }
            }
        }

        public static void TicketListCommmand(CommandArgs args)
        {
            int pagelimit = 5;
            int currentpage = 0;
            string[] officaltags = File.ReadAllText(tagpath).Split('\n');
            string tag = "All";
            string search = "All";

            if (args.Parameters.Count > 0)
            {
                if (args.Parameters[0].ToLower() == "help")
                {
                    args.Player.SendMessage("Syntax: /ticketlist <help/-t:<tag>/tags/-s:<keyword>> [pg#]", bluebase);
                    args.Player.SendMessage("- /ticketlist <help>: Shows this page.", bluesecondarybase);
                    args.Player.SendMessage("- /ticketlist <tag/all> <pg#>: Shows the tickets on the specified page/tag.", bluesecondarybase);
                    args.Player.SendMessage("- /ticketlist <tags>: Shows all the tags and a number of how many tickets are submitted for each tag.", bluesecondarybase);
                    args.Player.SendMessage("- /ticketlist <-s:<keyword>> <pg#>: Shows all tickets that contain the specified keyword.", bluesecondarybase);
                    args.Player.SendMessage("NOTE: If the ticket has been responded to, but not cleared by the player yet, you will see a \"{RESPONSE SENT}\" message next to it.", bluebase);
                }
                else if (args.Parameters[0].ToLower() == "tags")
                {
                    args.Player.SendMessage("Tags:", bluebase);
                    foreach (string officaltag in officaltags)
                    {
                        if (officaltag.StartsWith(" ") || officaltag == null || officaltag == "")
                            continue;
                        int i = 0;
                        foreach (StandardTicket ticket in ticketlist.Tickets)
                        {
                            if (ticket.getTag().Trim().ToLower() == officaltag.Trim().ToLower())
                                i++;
                        }
                        args.Player.SendMessage(string.Format("{0}: {1}", officaltag, i), bluesecondarybase);
                    }
                }
                else if (!int.TryParse(args.Parameters[0], out currentpage) || currentpage < 1)
                {
                    if (args.Parameters.Count > 1)
                    {
                        if (!int.TryParse(args.Parameters[1], out currentpage) || currentpage < 1)
                        {
                            args.Player.SendMessage(string.Format("Invalid page number ({0})", currentpage), Color.Red);
                            return;
                        }
                    }
                    else { currentpage = 1; }
                    if (args.Parameters[0].ToLower().StartsWith("-s:"))
                    {
                        search = args.Parameters[0].ToLower().Split(':')[1].Trim();
                    }
                    else if (args.Parameters[0].ToLower() != "all")
                    {
                        foreach (string officaltag in officaltags)
                        {
                            if (args.Parameters[0].Trim().ToLower() == officaltag.Trim().ToLower())
                            {
                                tag = officaltag;
                            }
                        }
                    }
                    else
                    {
                        args.Player.SendMessage("Could not find page or tag labeled " + args.Parameters[0], Color.Red);
                    }
                }
                currentpage--;
            }

            if (ticketlist.Tickets.Count < 1)
            {
                args.Player.SendMessage("There are no tickets submitted.", Color.Red);
                return;
            }

            var ticketslist = new List<string>();
            if (tag != "All")
            {
                for (int i = 0; i < ticketlist.Tickets.Count; i++)
                {
                    if ((ticketlist.Tickets[i].getResponse() == null) && (ticketlist.Tickets[i].getTag().Trim().ToLower() == tag.Trim().ToLower()))
                        ticketslist.Add((i + 1) + ". " + ticketlist.Tickets[i].getTime() + " - " + ticketlist.Tickets[i].getName() + ": " + ticketlist.Tickets[i].getTicket());
                    else if ((ticketlist.Tickets[i].getResponse() != null) && (ticketlist.Tickets[i].getTag().Trim().ToLower() == tag.Trim().ToLower()))
                        ticketslist.Add("{RESPONSE SENT} | " + (i + 1) + ". " + ticketlist.Tickets[i].getTime() + " - " + ticketlist.Tickets[i].getName() + ": " + ticketlist.Tickets[i].getTicket());
                }
            }
            else if (search != "All")
            {
                for (int i = 0; i < ticketlist.Tickets.Count; i++)
                {
                    if ((ticketlist.Tickets[i].getResponse() == null) && (ticketlist.Tickets[i].getTicket().ToLower().Contains(search.Trim())))
                        ticketslist.Add((i + 1) + ". " + ticketlist.Tickets[i].getTime() + " - " + ticketlist.Tickets[i].getName() + ": " + ticketlist.Tickets[i].getTicket());
                    else if ((ticketlist.Tickets[i].getResponse() != null) && (ticketlist.Tickets[i].getTicket().ToLower().Contains(search.Trim())))
                        ticketslist.Add("{RESPONSE SENT} | " + (i + 1) + ". " + ticketlist.Tickets[i].getTime() + " - " + ticketlist.Tickets[i].getName() + ": " + ticketlist.Tickets[i].getTicket());
                }
            }
            else
            {
                for (int i = 0; i < ticketlist.Tickets.Count; i++)
                {
                    if (ticketlist.Tickets[i].getResponse() == null)
                        ticketslist.Add("[" + ticketlist.Tickets[i].getTag() + "] " + (i + 1) + ". " + ticketlist.Tickets[i].getTime() + " - " + ticketlist.Tickets[i].getName() + ": " + ticketlist.Tickets[i].getTicket());
                    else
                        ticketslist.Add("{RESPONSE SENT} | [" + ticketlist.Tickets[i].getTag() + "] " + (i + 1) + ". " + ticketlist.Tickets[i].getTime() + " - " + ticketlist.Tickets[i].getName() + ": " + ticketlist.Tickets[i].getTicket());
                }
            }

            int pagecount = ticketslist.Count / pagelimit;

            if (currentpage > pagecount)
            {
                args.Player.SendMessage(string.Format("Page number exceeds pages ({0}/{1})", currentpage + 1, pagecount + 1), Color.Red);
                return;
            }

            if (ticketslist.Count == pagecount * pagelimit)
            {
                if (tag != "All")
                    args.Player.SendMessage(string.Format("Tickets with tag {0} ({1}/{2}):", tag, currentpage + 1, pagecount), bluebase);
                else if (search != "All")
                    args.Player.SendMessage(string.Format("Tickets with keyword {0} ({1}/{2}):", search, currentpage + 1, pagecount), bluebase);
                else
                    args.Player.SendMessage(string.Format("All Tickets ({0}/{1}):", currentpage + 1, pagecount), bluebase);
            }
            else
            {
                if (tag != "All")
                    args.Player.SendMessage(string.Format("Tickets with tag {0} ({1}/{2}):", tag, currentpage + 1, pagecount + 1), bluebase);
                else if (search != "All")
                    args.Player.SendMessage(string.Format("Tickets with keyword {0} ({1}/{2}):", search, currentpage + 1, pagecount + 1), bluebase);
                else
                    args.Player.SendMessage(string.Format("All Tickets ({0}/{1}):", currentpage + 1, pagecount + 1), bluebase);
            }
            
            var lines = ticketslist.ToArray();
            for (int i = (currentpage * pagelimit); (i < ((currentpage * pagelimit) + pagelimit)) && i < lines.Length; i++)
            {
                args.Player.SendMessage(lines[i], bluesecondarybase);
            }

            if ((currentpage < pagecount) && (((currentpage * pagelimit) + pagelimit) < lines.Length))
            {
                if (tag != "All")
                    args.Player.SendMessage(string.Format("Type \"/ticketlist {0} {1}\" for more tickets with the tag {0}.", tag, (currentpage + 2)), bluebase);
                else if (search != "All")
                    args.Player.SendMessage(string.Format("Type \"/ticketlist {0} {1}\" for more tickets with the keyword {0}.", args.Parameters[0], currentpage + 2), bluebase);
                else
                    args.Player.SendMessage(string.Format("Type \"/ticketlist {0}\" for more tickets.", (currentpage + 2)), bluebase);
            }
        }

        public static void TicketClear(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Syntax: /ticketclear <all/id/tag/response/help>", bluebase);
                args.Player.SendMessage("- /ticketclear <all>: Removes all the tickets", bluesecondarybase);
                args.Player.SendMessage("- /ticketclear <id> <idlist>: Removes one ticket ID based on the IDs listed with \"/ticketlist\" or if a list of ids is specified it will remove all the tickets ticket ids in that list.", bluesecondarybase);
                args.Player.SendMessage("EXAMPLE: \"/ticketclear id 1-5,7,10,12-15\" | Note that there is no use of spaces, and the ids must be in order from least to greatest, otherwise errors will occur.", bluesecondarybase);
                args.Player.SendMessage("A dash clears all tickets inbetween and including the 2 tickets specified.", bluesecondarybase);
                args.Player.SendMessage("- /ticketclear <tag> <tag>: Clears all tickets in the specified tag", bluesecondarybase);
                args.Player.SendMessage("- /ticketclear <response/r> <id> <message>: When the player who submitted that ticket logs in, he will recieve the message and the ticket will automatically be cleared.", bluesecondarybase);
                args.Player.SendMessage("Note: /ticketclear can be shortened to /ticclear", bluebase);
            }
            else
            {
                switch (args.Parameters[0].ToLower())
                {
                    case "all":
                        try
                        {
                            TicketReader reader = new TicketReader();
                            ticketlist = reader.writeFile(banned);
                            args.Player.SendMessage("All of the Tickets were cleared!", bluebase);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(string.Format("{0} has cleared all of the tickets.", args.Player.Name));
                            Console.ResetColor();
                            Log.Info(string.Format("{0} has cleared all of the tickets.", args.Player.Name));
                        }
                        catch (Exception e)
                        {
                            // Let the console know what went wrong, and tell the player that there was an error.
                            args.Player.SendMessage("There are no tickets!", Color.Red);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(e.Message);
                            Console.ResetColor();
                            Log.Error(e.Message);
                        }
                        break;
                    case "id":
                        if (args.Parameters.Count == 2)
                        {
                            string[] tickets = args.Parameters[1].Split(',');
                            for (int i = 0; i < tickets.Length; i++)
                            {
                                tickets[i] = tickets[i].ToLower().Trim();
                            }
                            List<string> ticketerrors = new List<string>();
                            List<string> ticketcompletions = new List<string>();
                            try
                            {
                                for (int i = tickets.Length - 1; i >= 0; i--)
                                {
                                    if (!tickets[i].Contains("-"))
                                    {
                                        try
                                        {
                                            if (Convert.ToInt32(tickets[i]) <= 0)
                                            {
                                                ticketerrors.Add(tickets[i] + " is not a valid ticket id");
                                            }
                                            if (Convert.ToInt32(tickets[i]) > ticketlist.Tickets.Count)
                                            {
                                                ticketerrors.Add(tickets[i] + " is greater than the last ticket id (" + ticketlist.Tickets.Count + ")");
                                            }
                                            else 
                                            { 
                                                lock (ticketlist.Tickets) 
                                                    ticketlist.Tickets.RemoveAt(Convert.ToInt32(tickets[i]) - 1);
                                                ticketcompletions.Add(tickets[i]);
                                            }
                                        }
                                        catch (FormatException)
                                        {
                                            ticketerrors.Add(tickets[i] + " is not in the correct format");
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            if ((Convert.ToInt32(tickets[i].Split('-')[1]) <= 0) || (Convert.ToInt32(tickets[i].Split('-')[0]) <= 0))
                                            {
                                                ticketerrors.Add(tickets[i] + " is not a valid id list");
                                            }
                                            if ((Convert.ToInt32(tickets[i].Split('-')[1]) > ticketlist.Tickets.Count) || (Convert.ToInt32(tickets[i].Split('-')[0]) > ticketlist.Tickets.Count))
                                            {
                                                ticketerrors.Add(tickets[i] + " has an id that is greater than the last ticket id (" + ticketlist.Tickets.Count + ")");
                                            }
                                            else
                                            {
                                                for (int i2 = Convert.ToInt32(tickets[i].Split('-')[1]) - 1; i2 >= Convert.ToInt32(tickets[i].Split('-')[0]) - 1; i2--)
                                                {
                                                    lock (ticketlist.Tickets)
                                                        ticketlist.Tickets.RemoveAt(i2);
                                                    if (i2 == Convert.ToInt32(tickets[i].Split('-')[0]) - 1)
                                                        ticketcompletions.Add(tickets[i]);
                                                }
                                            }
                                        }
                                        catch (FormatException)
                                        {
                                            ticketerrors.Add(tickets[i] + " is not in the correct format");
                                        }
                                    }
                                }
                                args.Player.SendMessage("ID(s): " + string.Join(", ", ticketcompletions) + " have been cleared successfully.", bluebase);
                                if (ticketerrors.Count > 0)
                                {
                                    args.Player.SendMessage("Errors: " + string.Join(", ", ticketerrors) + ".", Color.Red);
                                }
                                UpdateTicketsInJSON(save);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Write(args.Player.Name + " has cleared ticket ID(s): ");
                                Console.ResetColor();
                                Console.WriteLine(string.Join(", ", ticketcompletions));
                                Log.Info(string.Format("{0} has cleared ticket ID(s): {1}", args.Player.Name, string.Join(", ", ticketcompletions)));
                            }
                            catch (Exception e)
                            {
                                args.Player.SendMessage("Error, check logs for more details.", Color.Red);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(e.Message);
                                Console.ResetColor();
                                Log.Error(e.Message);
                            }
                        }
                        else { args.Player.SendMessage("Syntax: /ticketclear id <idlist>", Color.Red); }
                        break;
                    case "tag":
                        lock (ticketlist.Tickets)
                        {
                            try
                            {
                                string[] officialtags = File.ReadAllText(tagpath).Split('\n');
                                for (int i = 0; i < officialtags.Length; i++)
                                {
                                    officialtags[i] = officialtags[i].Trim().ToLower();
                                }
                                if (!((IList<string>)officialtags).Contains(args.Parameters[1].Trim().ToLower()))
                                {
                                    args.Player.SendMessage("Tag does not exist.", Color.Red);
                                    return;
                                }
                                for (int i = ticketlist.Tickets.Count - 1; i >= 0; i--)
                                {
                                    if (ticketlist.Tickets[i].getTag().Trim().ToLower() == args.Parameters[1].Trim().ToLower())
                                    {
                                        ticketlist.Tickets.Remove(ticketlist.Tickets[i]);
                                    }
                                }
                                UpdateTicketsInJSON(save);
                            }
                            catch (Exception e)
                            {
                                args.Player.SendMessage("Error, could not clear tickets, check log for more info.", Color.Red);
                                Log.Error(e.Message);
                            }
                            finally
                            {
                                args.Player.SendMessage("All tickets with the tag \"" + args.Parameters[1] + "\" cleared.", bluebase); 
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Write(args.Player.Name + " has cleared all tickets with tag: ");
                                Console.ResetColor();
                                Console.WriteLine(args.Parameters[1]);
                                Log.Info(string.Format("{0} has cleared all tickets with tag: {1}", args.Player.Name, args.Parameters[1]));
                            }
                        }
                        break;
                    case "response":
                        if (args.Parameters.Count > 1)
                        {
                            int i = 0;
                            string respond = "";
                            foreach (string arg in args.Parameters)
                            {
                                i++;
                                if (i > 2)
                                {
                                    respond = respond + arg + " ";
                                }
                            }
                            int lineToRespond = 0;
                            int.TryParse(args.Parameters[1], out lineToRespond);
                            string playername = ticketlist.Tickets[lineToRespond - 1].getName();
                            string ticket = ticketlist.Tickets[lineToRespond - 1].getTicket();
                            try
                            {
                                var listedplayer = Player.GetPlayerByName(playername);
                                listedplayer.TSPlayer.SendMessage("Your ticket (" + ticket.Trim() + ") has been responded to: ", bluebase);
                                listedplayer.TSPlayer.SendMessage(respond.Trim(), bluesecondarybase);
                                ticketlist.Tickets.RemoveAt(lineToRespond - 1);
                                UpdateTicketsInJSON(save);
                            }
                            catch
                            {
                                try
                                {
                                    ticketlist.Tickets[lineToRespond - 1].setResponse(respond);
                                    UpdateTicketsInJSON(save);
                                }
                                catch (Exception e)
                                {
                                    args.Player.SendMessage("Your response could not be sent.", Color.Red);
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine(e.Message);
                                    Console.ResetColor();
                                    Log.Error(e.Message);
                                }
                            }
                            finally { args.Player.SendMessage("You just responded to Ticket ID: " + lineToRespond, bluebase); }
                        }
                        break;
                    case "r":
                        goto case "response";
                    case "help":
                        args.Player.SendMessage("Syntax: /ticketclear <all/id/tag/response/help>", bluebase);
                        args.Player.SendMessage("- /ticketclear <all>: Removes all the tickets", bluesecondarybase);
                        args.Player.SendMessage("- /ticketclear <id> <idlist>: Removes one ticket ID based on the IDs listed with \"/ticketlist\" or if a list of ids is specified it will remove all the tickets ticket ids in that list.", bluesecondarybase);
                        args.Player.SendMessage("EXAMPLE: \"/ticketclear id 1-5,7,10,12-15\" | Note that there is no use of spaces, and the ids must be in order from least to greatest, otherwise errors will occur.", bluesecondarybase);
                        args.Player.SendMessage("A dash clears all tickets inbetween and including the 2 tickets specified.", bluesecondarybase);
                        args.Player.SendMessage("- /ticketclear <tag> <tag>: Clears all tickets in the specified tag", bluesecondarybase);
                        args.Player.SendMessage("- /ticketclear <response/r> <id> <message>: When the player who submitted that ticket logs in, he will recieve the message and the ticket will automatically be cleared.", bluesecondarybase);
                        args.Player.SendMessage("Note: /ticketclear can be shortened to /ticclear", bluebase);
                        break;
                    default:
                        args.Player.SendMessage("Syntax: /ticketclear <all/id/tag/response/help>", Color.Red);
                        break;
                }
            }
        }

        public static void TicBan(CommandArgs args)
        {
            string tempbanned = Path.Combine(TShock.SavePath, @"TicketSystem\tempbanned.txt");
            int numberOfPeopleBanned = 1;
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Syntax: /ticketban <ban/unban/list/help>", bluebase);
                args.Player.SendMessage("- /ticketban <ban> <player name>: stops the player from filing tickets", bluesecondarybase);
                args.Player.SendMessage("- /ticketban <unban> <player id/name>: unbans a player based on their ID or their name. Use /ticban list to find out banned IDs", bluesecondarybase);
                args.Player.SendMessage("- /ticketban <list>: lists players that are banned and their IDs", bluesecondarybase);
                args.Player.SendMessage("Note: /ticketban can be shortened to /ticban", bluebase);
            }
            else
            {
                switch (args.Parameters[0].ToLower())
                {
                    case "ban":
                        if (args.Parameters.Count == 1)
                        {
                            args.Player.SendMessage("/ticketban ban <player name>: stops the player from filing tickets", bluebase);
                        }
                        else
                        {
                            try
                            {
                                var FindPlayer = TShock.Utils.FindPlayer(args.Parameters[1]);
                                var ListedPlayer = Player.GetPlayerByName(args.Parameters[1]);
                                if (FindPlayer.Count == 1)
                                {
                                    if ((ListedPlayer.GetTicState() == Player.CanSubmitTickets.yes) && (!FindPlayer[0].Group.HasPermission("TicketBan")))
                                    {
                                        ListedPlayer.SetTicState(Player.CanSubmitTickets.no);
                                        args.Player.SendMessage(string.Format("You have revoked the privileges of submitting tickets from {0}", FindPlayer[0].Name), Color.Red);
                                        using (StreamWriter writer = new StreamWriter(banned))
                                        {
                                            writer.WriteLine(FindPlayer[0].Name.ToLower());
                                        }
                                        FindPlayer[0].SendMessage("You can no longer submit tickets.", Color.Red);
                                    }
                                    else if (FindPlayer[0].Group.HasPermission("TicketBan"))
                                    {
                                        args.Player.SendMessage("This player cannot be banned from using tickets.", Color.Red);
                                        Log.Info(args.Player.Name + " tried to ban " + FindPlayer[0].Name + " from using tickets.");
                                    }
                                    else
                                    {
                                        args.Player.SendMessage("This player already is banned from using tickets. If you want to give back the privilege, use /ticetban unban <player id/name> (find banned IDs by doing /ticketban list)", Color.Red);
                                    }
                                }
                                else if (FindPlayer.Count > 1)
                                {
                                    args.Player.SendMessage(string.Format("There is more than one person online with the name {0}", args.Parameters[1]), Color.Red);
                                }
                                else if (FindPlayer.Count < 1)
                                {
                                    args.Player.SendMessage(string.Format("There is nobody online with the name {0}", args.Parameters[1]), Color.Red);
                                }
                            }
                            catch (Exception e)
                            {
                                args.Player.SendMessage(string.Format("There is nobody online with the name {0}", args.Parameters[1]), Color.Red);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(e.Message);
                                Console.ResetColor();
                                Log.Error(e.Message);
                            }
                        }
                        break;
                    case "unban":
                        if (args.Parameters.Count == 1)
                        {
                            args.Player.SendMessage("/ticketban unban <player id/name>: unbans a player based on their ID or their name. Use /ticban list to find out banned IDs", bluebase);
                        }
                        else
                        {
                            int id;
                            try
                            {
                                int.TryParse(args.Parameters[1], out id);
                                if (id == 0)
                                {
                                    string line = null;
                                    string line_to_delete = args.Parameters[1];
                                    int i = 0;

                                    StreamReader reader = new StreamReader(banned);
                                    StreamWriter writer = new StreamWriter(tempbanned, true);
                                    while ((line = reader.ReadLine()) != null)
                                    {
                                        if (String.Compare(line, line_to_delete, true) != 0)
                                        {
                                            writer.WriteLine(line);
                                        }
                                        else
                                        {
                                            i++;
                                        }
                                    }
                                    reader.Close();
                                    writer.Close();
                                    File.Delete(banned);
                                    File.Move(tempbanned, banned);
                                    if (i == 0)
                                    {
                                        args.Player.SendMessage(string.Format("Cannot find player name {0}", args.Parameters[1]), Color.Red);
                                    }
                                    else
                                    {
                                        args.Player.SendMessage(string.Format("You have given back the privileges of submitting tickets to player: {0}. This will take affect when they next log in.", args.Parameters[1]), bluebase);
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.Write(args.Player.Name + " has unbanned player: ");
                                        Console.ResetColor();
                                        Console.WriteLine(args.Parameters[1]);
                                        Log.Info(string.Format("{0} has unbanned player: {1}", args.Player.Name, args.Parameters[1]));
                                        i = 0;
                                    }
                                }
                                else
                                {
                                    var file = new List<string>(System.IO.File.ReadAllLines(banned));
                                    file.RemoveAt(id - 1);
                                    File.WriteAllLines(banned, file.ToArray());
                                    args.Player.SendMessage(string.Format("You have given back the privileges of submitting tickets to player ID: {0}. This will take affect when they next log in.", args.Parameters[1]), bluebase);
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.Write(args.Player.Name + " has unbanned player ID: ");
                                    Console.ResetColor();
                                    Console.WriteLine(args.Parameters[1]);
                                    Log.Info(string.Format("{0} has unbanned player ID: {1}", args.Player.Name, args.Parameters[1]));
                                }
                            }
                            catch (Exception e)
                            {
                                args.Player.SendMessage(string.Format("Cannot find player {0}", args.Parameters[1]), Color.Red);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(e.Message);
                                Console.ResetColor();
                                Log.Error(e.Message);
                            }
                        }
                        break;
                    case "list":
                        try
                        {
                            StreamReader sr = new StreamReader(banned);
                            while (sr.Peek() >= 0)
                            {
                                args.Player.SendMessage(numberOfPeopleBanned + ". " + sr.ReadLine(), bluesecondarybase);
                                numberOfPeopleBanned++;
                            }
                            sr.Close();
                            numberOfPeopleBanned = 1;
                        }
                        catch (Exception e)
                        {
                            args.Player.SendMessage("Nobody is banned from using tickets.", Color.Red);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(e.Message);
                            Console.ResetColor();
                            Log.Error(e.Message);
                        }
                        break;
                    case "help":
                        args.Player.SendMessage("Syntax: /ticketban <ban/unban/list/help>", bluebase);
                        args.Player.SendMessage("- /ticketban <ban> <player name>: stops the player from filing tickets", bluesecondarybase);
                        args.Player.SendMessage("- /ticketban <unban> <player id/name>: unbans a player based on their ID or their name. Use /ticban list to find out banned IDs", bluesecondarybase);
                        args.Player.SendMessage("- /ticketban <list>: lists players that are banned and their IDs", bluesecondarybase);
                        args.Player.SendMessage("Note: /ticketban can be shortened to /ticban", bluebase);
                        break;
                    default:
                        args.Player.SendMessage("Syntax: /ticketban <ban/unban/list>", Color.Red);
                        break;
                }
            }
        }

        public static void Reload(CommandArgs args)
        {
            TicketReader Reader = new TicketReader();
            try
            {
                ticketlist = Reader.readFile(save);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Ticket System being reloaded...");
                args.Player.SendMessage("Ticket System being reloaded...", Color.Yellow);
                Log.Info("Ticket System Reloading...");
                if (ticketlist.Tickets.Count != 0)
                {
                    Console.WriteLine(ticketlist.Tickets.Count + " tickets have been reloaded.");
                    args.Player.SendMessage(ticketlist.Tickets.Count + " tickets have been reloaded.", Color.Yellow);
                }
                else
                {
                    Console.WriteLine("There are no tickets.");
                    args.Player.SendMessage("There are no tickets.", Color.Yellow);
                }
                Console.ResetColor();
                args.Player.SendMessage("Ticket System reloaded.", Color.Yellow);
                Log.Info("Ticket System Reloaded.");
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in Tickets.json file! Check log for more details.");
                args.Player.SendMessage("Error in Tickets.json file! Check log for more details.", Color.Red);
                Console.WriteLine(e.Message);
                Console.ResetColor();
                Log.Error("--------- Config Exception in TicketSystem Config file (Tickets.json) ---------");
                Log.Error(e.Message);
                Log.Error("---------------------------------- Error End ----------------------------------");
            }
        }
        #endregion

        #region Extra stuff
        public bool UpdateChecker()
        {
            string raw;
            try
            {
                raw = new WebClient().DownloadString("https://github.com/Spectrewiz/Ticket-System/raw/master/README.txt");
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                return false;
            }
            string[] readme = raw.Split('\n');
            string[] download = readme[readme.Length - 1].Split('-');
            Version version;
            if (!Version.TryParse(readme[0], out version)) return false;
            if (Version.CompareTo(version) >= 0) return false;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("New Ticket System version: "); Console.ResetColor(); Console.WriteLine(readme[0].Trim());
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Download here: "); Console.ResetColor(); Console.WriteLine(download[1].Trim());
            Log.Info(string.Format("NEW VERSION: {0}  |  Download here: {1}", readme[0].Trim(), download[1].Trim()));
            downloadFromUpdate = download[1].Trim();
            versionFromUpdate = readme[0].Trim();
            return true;
        }

        public static void UpdateTicketsInJSON(string savepath)
        {
            File.Delete(savepath);
            TextWriter tw = new StreamWriter(savepath, true);
            tw.Write(JsonConvert.SerializeObject(ticketlist, Formatting.Indented));
            tw.Close();
        }
        #endregion
    }
}