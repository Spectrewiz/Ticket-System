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
    [APIVersion(1, 11)]
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
            get { return new Version(1, 1, 2); }
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
            Commands.ChatCommands.Add(new Command(Hlpme, "ticket", "hlpme"));
            Commands.ChatCommands.Add(new Command("TicketList", TicketListCommmand, "ticketlist", "ticlist"));
            Commands.ChatCommands.Add(new Command("TicketClear", TicketClear, "ticketclear", "ticketsclear", "ticclear", "ticsclear"));
            Commands.ChatCommands.Add(new Command("TicketBan", TicBan, "ticketban", "ticban"));

            save = Path.Combine(TShock.SavePath, @"TicketSystem\Tickets.json");
            banned = Path.Combine(TShock.SavePath, @"TicketSystem\Banned.txt");
            TicketReader Reader = new TicketReader();

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
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error in Tickets.json file! Check log for more details.");
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                    Log.Error("--------- Config Exception in TicketSystem Config file (Tickets.json) ---------");
                    Log.Error(e.Message);
                    Log.Error("---------------------------------- Error End ----------------------------------");
                }
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(TShock.SavePath, "TicketSystem"));
                ticketlist = Reader.writeFile(save);
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
            var ListedPlayer = Player.GetPlayerByName(name);
            int count = ticketlist.Tickets.Count;
            if (File.Exists(save))
            {
                int i = ticketlist.responseCount(TShock.Players[who].Name);
                int x = ticketlist.ticketCount(TShock.Players[who].Name);
                if (i != 0)
                {
                    TShock.Players[who].SendMessage("--- Responses for Tickets (" + i + "/" + x + ") ---", 30, 144, 255);
                    foreach (StandardTicket t in ticketlist.Tickets)
                    {
                        if (t.getName() == TShock.Players[who].Name)
                        {
                            if (t.getResponse() != null)
                            {
                                TShock.Players[who].SendMessage("(" + t.getTicket().Trim() + "): " + t.getResponse(), 135, 206, 255);
                            }
                        }
                    }
                    TShock.Players[who].SendMessage("To see the responses again, type /ticket responses", 30, 144, 255);
                    TShock.Players[who].SendMessage("To clear the responses, type /ticket clearresponses", 30, 144, 255);
                }
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
            if (!TShock.Players[who].Group.HasPermission("TicketList") && (ListedPlayer.GetTicState() != Player.CanSubmitTickets.no) && (ticketlist.responseCount(name) < 1))
                TShock.Players[who].SendMessage("To write a complaint, use /ticket <Message>", 30, 144, 255);
            else if ((TShock.Players[who].Group.HasPermission("TicketList")) && (count != 0))
                TShock.Players[who].SendMessage("There are " + count + " tickets submitted, use /ticketlist to view them.", 30, 144, 255);
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
        public static void Hlpme(CommandArgs args)
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
                    foreach (StandardTicket t in ticketlist.Tickets)
                    {
                        if (t.getName() == args.Player.Name)
                        {
                            if (t.getResponse() != null)
                                ticketlist.Tickets.Remove(t);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
                File.Delete(save);
                TextWriter tw = new StreamWriter(save, true);
                tw.Write(JsonConvert.SerializeObject(ticketlist, Formatting.Indented));
                tw.Close();
                args.Player.SendMessage("Your responses have been cleared!", 30, 144, 255);
            }
            else if (args.Parameters[0].ToLower() == "responses")
            {
                if (File.Exists(save))
                {
                    int i = ticketlist.responseCount(args.Player.Name);
                    int x = ticketlist.ticketCount(args.Player.Name);
                    if (i != 0)
                    {
                        args.Player.SendMessage("--- Responses for Tickets (" + i + "/" + x + ") ---", 30, 144, 255);
                        foreach (StandardTicket t in ticketlist.Tickets)
                        {
                            if (t.getName() == args.Player.Name)
                            {
                                if (t.getResponse() != null)
                                {
                                    args.Player.SendMessage("(" + t.getTicket().Trim() + "): " + t.getResponse(), 135, 206, 255);
                                }
                            }
                        }
                        args.Player.SendMessage("To clear the responses, type /ticket clearresponses", 30, 144, 255);
                    }
                }
            }
            else
            {
                try
                {
                    string text = "";
                    foreach (string word in args.Parameters)
                    {
                        text = text + word + " ";
                    }

                    ticketlist.AddItem(new StandardTicket(args.Player.Name, text, null, DateTime.Now.ToString()));

                    File.Delete(save);
                    TextWriter tw = new StreamWriter(save, true);
                    tw.Write(JsonConvert.SerializeObject(ticketlist, Formatting.Indented));
                    tw.Close();

                    args.Player.SendMessage("Your Ticket has been sent!", 30, 144, 255);
                    foreach (Player player in TicketSystem.Players)
                    {
                        if (player.TSPlayer.Group.HasPermission("TicketList"))
                        {
                            player.TSPlayer.SendMessage(string.Format("{0} just submitted a ticket: {1}", args.Player.Name, text), 30, 144, 255);
                        }
                    }
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
            int pglimit = 5;
            int linelmt = 1;
            int crntpage = 0;
            
            if (args.Parameters.Count > 0)
            {
                if (args.Parameters[0].ToLower() == "help")
                {
                    args.Player.SendMessage("Syntax: /ticketlist <help/pg#>", 30, 144, 255);
                    args.Player.SendMessage("- /ticketlist <help>: Shows this page.", 135, 206, 255);
                    args.Player.SendMessage("- /ticketlist <pg#>: Shows the tickets on the specified page.", 135, 206, 255);
                    args.Player.SendMessage("NOTE: If the ticket has been responded to, but not cleared by the player yet, you will see a \"[RESPONSE SEND\" message next to it.", 30, 144, 255);
                }
                else if (!int.TryParse(args.Parameters[0], out crntpage) || crntpage < 1)
                {
                    args.Player.SendMessage(string.Format("Invalid page number ({0})", crntpage), Color.Red);
                    return;
                }
                crntpage--;
            }

            if (ticketlist.Tickets.Count < 1)
            {
                args.Player.SendMessage("There are no tickets submitted.", Color.Red);
                return;
            }

            int pgcount = ticketlist.Tickets.Count / pglimit;
            if (crntpage > pgcount)
            {
                args.Player.SendMessage(string.Format("Page number exceeds pages ({0}/{1})", crntpage + 1, pgcount + 1), Color.Red);
                return;
            }

            args.Player.SendMessage(string.Format("Tickets ({0}/{1}):", crntpage + 1, pgcount + 1), 30, 144, 255);

            var ticketslist = new List<string>();
            for (int i = (crntpage * pglimit); (i < ((crntpage * pglimit) + pglimit)) && i < ticketlist.Tickets.Count; i++)
            {
                if (ticketlist.Tickets[i].getResponse() == null)
                {
                    ticketslist.Add((i + 1) + ". " + ticketlist.Tickets[i].getTime() + " - " + ticketlist.Tickets[i].getName() + ": " + ticketlist.Tickets[i].getTicket());
                }
                else
                    ticketslist.Add("[RESPONSE SENT] | " + (i + 1) + ". " + ticketlist.Tickets[i].getTime() + " - " + ticketlist.Tickets[i].getName() + ": " + ticketlist.Tickets[i].getTicket());
            }
            var lines = ticketslist.ToArray();
            for (int i = 0; i < lines.Length; i += linelmt)
            {
                args.Player.SendMessage(string.Join(", ", lines, i, Math.Min(lines.Length - i, linelmt)), 135, 206, 255);
            }

            if (crntpage < pgcount)
            {
                args.Player.SendMessage(string.Format("Type /ticketlist {0} for more tickets.", (crntpage + 2)), 30, 144, 255);
            }
        }

        public static void TicketClear(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Syntax: /ticketclear <all/id/response/help>", 30, 144, 255);
                args.Player.SendMessage("- /ticketclear <all>: Removes all the tickets", 135, 206, 255);
                args.Player.SendMessage("- /ticketclear <id> <id>: Removes one ticket ID based on the IDs listed with /ticketlist", 135, 206, 255);
                args.Player.SendMessage("- /ticketclear <response/r> <id> <message>: When the player who submitted that ticket logs in, he will recieve the message and the ticket will automatically be cleared.", 135, 206, 255);
                args.Player.SendMessage("Note: /ticketclear can be shortened to /ticclear", 30, 144, 255);
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
                            args.Player.SendMessage("All of the Tickets were cleared!", 30, 144, 255);
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
                        if (args.Parameters.Count > 1)
                        {
                            try
                            {
                                int lineToDelete = (Convert.ToInt32(args.Parameters[1]) - 1);
                                ticketlist.Tickets.RemoveAt(lineToDelete);
                                File.Delete(save);
                                TextWriter tw = new StreamWriter(save, true);
                                tw.Write(JsonConvert.SerializeObject(ticketlist, Formatting.Indented));
                                tw.Close();
                                args.Player.SendMessage(string.Format("Ticket ID {0} was cleared!", args.Parameters[1]), 30, 144, 255);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(string.Format("{0} has cleared ticket ID: {1}", args.Player.Name, args.Parameters[1]));
                                Console.ResetColor();
                                Log.Info(string.Format("{0} has cleared ticket ID: {1}", args.Player.Name, args.Parameters[1]));
                            }
                            catch (Exception e)
                            {
                                args.Player.SendMessage("Not a valid ID.", Color.Red);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(e.Message);
                                Console.ResetColor();
                                Log.Error(e.Message);
                            }
                        }
                        else
                        {
                            args.Player.SendMessage("You have to state a ticket ID! Syntax: /ticclear id <id>", Color.Red);
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
                                listedplayer.TSPlayer.SendMessage("Your ticket (" + ticket.Trim() + ") has been responded to: ", 30, 144, 255);
                                listedplayer.TSPlayer.SendMessage(respond.Trim(), 135, 206, 255);
                                ticketlist.Tickets.RemoveAt(lineToRespond - 1);
                                File.Delete(save);
                                TextWriter tw = new StreamWriter(save, true);
                                tw.Write(JsonConvert.SerializeObject(ticketlist, Formatting.Indented));
                                tw.Close();
                            }
                            catch
                            {
                                try
                                {
                                    ticketlist.Tickets[lineToRespond - 1].setResponse(respond);
                                    File.Delete(save);
                                    TextWriter tw = new StreamWriter(save, true);
                                    tw.Write(JsonConvert.SerializeObject(ticketlist, Formatting.Indented));
                                    tw.Close();
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
                            finally { args.Player.SendMessage("You just responded to Ticket ID: " + lineToRespond, 30, 144, 255); }
                        }
                        break;
                    case "r":
                        goto case "response";
                    case "help":
                        args.Player.SendMessage("Syntax: /ticketclear <all/id/response/help>", 30, 144, 255);
                        args.Player.SendMessage("- /ticketclear <all>: Removes all the tickets", 135, 206, 255);
                        args.Player.SendMessage("- /ticketclear <id> <id>: Removes one ticket ID based on the IDs listed with /ticketlist", 135, 206, 255);
                        args.Player.SendMessage("- /ticketclear <response/r> <id> <message>: When the player who submitted that ticket logs in, he will recieve the message and the ticket will automatically be cleared.", 135, 206, 255);
                        args.Player.SendMessage("Note: /ticketclear can be shortened to /ticclear", 30, 144, 255);
                        break;
                    default:
                        args.Player.SendMessage("Syntax: /ticketclear <all/id/response>", Color.Red);
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
                args.Player.SendMessage("Syntax: /ticketban <ban/unban/list/help>", 30, 144, 255);
                args.Player.SendMessage("- /ticketban <ban> <player name>: stops the player from filing tickets", 135, 206, 255);
                args.Player.SendMessage("- /ticketban <unban> <player id/name>: unbans a player based on their ID or their name. Use /ticban list to find out banned IDs", 135, 206, 255);
                args.Player.SendMessage("- /ticketban <list>: lists players that are banned and their IDs", 135, 206, 255);
                args.Player.SendMessage("Note: /ticketban can be shortened to /ticban", 30, 144, 255);
            }
            else
            {
                switch (args.Parameters[0].ToLower())
                {
                    case "ban":
                        if (args.Parameters.Count == 1)
                        {
                            args.Player.SendMessage("/ticketban ban <player name>: stops the player from filing tickets", 30, 144, 255);
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
                            args.Player.SendMessage("/ticketban unban <player id/name>: unbans a player based on their ID or their name. Use /ticban list to find out banned IDs", 30, 144, 255);
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
                                        args.Player.SendMessage(string.Format("You have given back the privileges of submitting tickets to player: {0}. This will take affect when they next log in.", args.Parameters[1]), 30, 144, 255);
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.WriteLine(string.Format("{0} has unbanned player: {1}", args.Player.Name, args.Parameters[1]));
                                        Console.ResetColor();
                                        Log.Info(string.Format("{0} has unbanned player: {1}", args.Player.Name, args.Parameters[1]));
                                        i = 0;
                                    }
                                }
                                else
                                {
                                    var file = new List<string>(System.IO.File.ReadAllLines(banned));
                                    file.RemoveAt(id - 1);
                                    File.WriteAllLines(banned, file.ToArray());
                                    args.Player.SendMessage(string.Format("You have given back the privileges of submitting tickets to player ID: {0}. This will take affect when they next log in.", args.Parameters[1]), 30, 144, 255);
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine(string.Format("{0} has unbanned player ID: {1}", args.Player.Name, args.Parameters[1]));
                                    Console.ResetColor();
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
                                args.Player.SendMessage(numberOfPeopleBanned + ". " + sr.ReadLine(), 135, 206, 255);
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
                        args.Player.SendMessage("Syntax: /ticketban <ban/unban/list/help>", 30, 144, 255);
                        args.Player.SendMessage("- /ticketban <ban> <player name>: stops the player from filing tickets", 135, 206, 255);
                        args.Player.SendMessage("- /ticketban <unban> <player id/name>: unbans a player based on their ID or their name. Use /ticban list to find out banned IDs", 135, 206, 255);
                        args.Player.SendMessage("- /ticketban <list>: lists players that are banned and their IDs", 135, 206, 255);
                        args.Player.SendMessage("Note: /ticketban can be shortened to /ticban", 30, 144, 255);
                        break;
                    default:
                        args.Player.SendMessage("Syntax: /ticketban <ban/unban/list>", Color.Red);
                        break;
                }
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
            Console.WriteLine("New Ticket System version: " + readme[0].Trim());
            Console.WriteLine("Download here: " + download[1].Trim());
            Console.ResetColor();
            Log.Info(string.Format("NEW VERSION: {0}  |  Download here: {1}", readme[0].Trim(), download[1].Trim()));
            downloadFromUpdate = download[1].Trim();
            versionFromUpdate = readme[0].Trim();
            return true;
        }
        #endregion
    }
}