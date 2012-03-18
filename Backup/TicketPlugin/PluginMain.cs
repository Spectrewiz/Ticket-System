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

namespace TicketPlugin
{
    [APIVersion(1, 11)]
    public class TicketPlugin : TerrariaPlugin
    {
        #region Main Plugin
        public static List<Player> Players = new List<Player>();
        public override string Name
        {
            get { return "TicketSystem"; }
        }

        public override string Author
        {
            get { return "Spectrewiz"; }
        }

        public override string Description
        {
            get { return "This plugin allows users in game to file tickets (complaints) that admins and moderators can access."; }
        }

        public override Version Version
        {
            get { return new Version(1, 0, 0); }
        }

        public override void Initialize()
        {
            GameHooks.Update += OnUpdate;
            GameHooks.Initialize += OnInitialize;
            NetHooks.GreetPlayer += OnGreetPlayer;
            ServerHooks.Leave += OnLeave;
            ServerHooks.Chat += OnChat;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Update -= OnUpdate;
                GameHooks.Initialize -= OnInitialize;
                NetHooks.GreetPlayer -= OnGreetPlayer;
                ServerHooks.Leave -= OnLeave;
                ServerHooks.Chat -= OnChat;
            }
            base.Dispose(disposing);
        }

        public TicketPlugin(Main game)
            : base(game)
        {
            Order = 10;
        }

        public void OnInitialize()
        {
            bool tic = false;

            foreach (Group group in TShock.Groups.groups)
            {
                if (group.Name != "superadmin")
                {
                    if (group.HasPermission("TicketList"))
                        tic = true;
                }
            }

            List<string> permlist = new List<string>();
            if (!tic)
            {
                permlist.Add("TicketList");
                permlist.Add("TicketClear");
                permlist.Add("TicketBan");
            }

            TShock.Groups.AddPermissions("trustedadmin", permlist);

            Commands.ChatCommands.Add(new Command(Hlpme, "hlpme", "ticket"));
            Commands.ChatCommands.Add(new Command("TicketList", TicketList, "ticketlist", "ticlist"));
            Commands.ChatCommands.Add(new Command("TicketClear", TicketClear, "ticketclear", "ticketsclear", "ticclear", "ticsclear"));
            Commands.ChatCommands.Add(new Command("TicketBan", TicBan, "ticketban", "ticban"));
        }

        public void OnUpdate()
        {
        }

        public static int NumberOfTickets(string name)
        {
            if (name != null && File.Exists("Tickets.txt"))
            {
                int count = 0;
                StreamReader sr = new StreamReader("Tickets.txt", true);
                while (sr.Peek() >= 0)
                {
                    sr.ReadLine();
                    count++;
                }
                sr.Close();
                return count;
            }
            return 0;
        }

        public void OnGreetPlayer(int who, HandledEventArgs e)
        {
            lock (Players)
                Players.Add(new Player(who));
            string name = TShock.Players[who].Name.ToLower();
            string line;
            var ListedPlayer = Player.GetPlayerByName(name);
            int count = NumberOfTickets(name);
            if (!TShock.Players[who].Group.HasPermission("TicketList"))
            {
                TShock.Players[who].SendMessage("To write a Complaint, use /hlpme ''<Message>''", Color.DarkCyan);
            }
            else if (TShock.Players[who].Group.HasPermission("TicketList"))
            {
                TShock.Players[who].SendMessage("There are " + count + " tickets submitted, use /ticketlist to view them.", Color.Cyan);
            }
            if (File.Exists(@"tshock\bannedfromtics.txt"))
            {
                using (StreamReader reader = new StreamReader(@"tshock\bannedfromtics.txt"))
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
                        break; //Found the player, break.
                    }
                }
            }
        }

        public void OnChat(messageBuffer msg, int ply, string text, HandledEventArgs e)
        {
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
            else if ((FindMe.GetTicState() == Player.CanSubmitTickets.yes) && ((args.Parameters.Count == 1) && (args.Parameters[0].ToLower() == "help")))
            {
                args.Player.SendMessage("To file a complaint about a bug or just a general issue that you have, do /hlpme <message>", Color.Cyan);
            }
            else if ((FindMe.GetTicState() == Player.CanSubmitTickets.yes) && (args.Parameters.Count < 1))
            {
                args.Player.SendMessage("You must enter a message!", Color.Red);
            }
            else if ((FindMe.GetTicState() == Player.CanSubmitTickets.yes) && ((args.Parameters.Count >= 1) || (args.Parameters.Count == 1 && args.Parameters[0].ToLower() != "help")))
            {
                try
                {
                    string text = "";
                    foreach (string word in args.Parameters)
                    {
                        text += word + " ";
                    }
                    string username = args.Player.Name;
                    args.Player.SendMessage("Your Ticket has been sent!", Color.DarkCyan);
                    StreamWriter tw = new StreamWriter("Tickets.txt", true);
                    tw.WriteLine(string.Format("{0} - {1}: {2}", DateTime.Now, username, text));
                    tw.Close();
                    foreach (Player player in TicketPlugin.Players)
                    {
                        if (player.TSPlayer.Group.HasPermission("TicketList"))
                        {
                            player.TSPlayer.SendMessage(string.Format("{0} just submitted a ticket: {1}", args.Player.Name, text), Color.Cyan);
                        }
                    }
                }
                catch (Exception e)
                {
                    args.Player.SendMessage("Your ticket could not be sent, contact an administrator.", Color.Red);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                }
            }
        }

        public static void TicketList(CommandArgs args)
        {
            int linenumber = 1;
            try
            {
                StreamReader sr = new StreamReader("Tickets.txt");
                while (sr.Peek() >= 0)
                {
                    args.Player.SendMessage(linenumber + ". " + sr.ReadLine(), Color.Cyan);
                    linenumber++;
                }
                sr.Close();
                linenumber = 1;
            }
            catch (Exception e)
            {
                // Let the console know what went wrong, and tell the player that the file could not be read.
                args.Player.SendMessage("The file could not be read, or it doesnt exist.", Color.Red);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ResetColor();
            }
        }

        public static void TicketClear(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Syntax: /ticketclear <all/id> <id>", Color.DarkCyan);
                args.Player.SendMessage("/ticketclear all: Removes all the tickets", Color.DarkCyan);
                args.Player.SendMessage("/ticketclear id <id>: Removes one ticket ID based on the IDs listed with /ticketlist", Color.DarkCyan);
                args.Player.SendMessage("Note: /ticketclear can be shortened to /ticclear", Color.DarkCyan);
            }
            else
            {
                switch (args.Parameters[0].ToLower())
                {
                    case "all":
                        try
                        {
                            File.Delete("Tickets.txt");
                            args.Player.SendMessage("All of the Tickets were cleared!", Color.DarkCyan);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(string.Format("{0} has cleared all of the tickets.", args.Player.Name));
                            Console.ResetColor();
                        }
                        catch (Exception e)
                        {
                            // Let the console know what went wrong, and tell the player that there was an error.
                            args.Player.SendMessage("All the tickets are already cleared!", Color.Red);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(e.Message);
                            Console.ResetColor();
                        }
                        break;
                    case "id":
                        if (args.Parameters.Count > 1)
                        {
                            try
                            {
                                int lineToDelete = (Convert.ToInt32(args.Parameters[1]) - 1);
                                var file = new List<string>(System.IO.File.ReadAllLines("Tickets.txt"));
                                file.RemoveAt(lineToDelete);
                                File.WriteAllLines("Tickets.txt", file.ToArray());
                                args.Player.SendMessage(string.Format("Ticket ID {0} was cleared!", args.Parameters[1]), Color.DarkCyan);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(string.Format("{0} has cleared ticket ID: {1}", args.Player.Name, args.Parameters[1]));
                                Console.ResetColor();
                            }
                            catch (Exception e)
                            {
                                args.Player.SendMessage("Not a valid ID.", Color.Red);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(e.Message);
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            args.Player.SendMessage("You have to state a ticket ID! Syntax: /ticclear id <ticID>", Color.Red);
                        }
                        break;
                    case "help":
                        args.Player.SendMessage("Syntax: /ticketclear <all/id> <id>", Color.DarkCyan);
                        args.Player.SendMessage("/ticketclear all: Removes all the tickets", Color.DarkCyan);
                        args.Player.SendMessage("/ticketclear id <id>: Removes one ticket ID based on the IDs listed with /ticketlist", Color.DarkCyan);
                        args.Player.SendMessage("Note: /ticketclear can be shortened to /ticclear", Color.DarkCyan);
                        break;
                    default:
                        args.Player.SendMessage("Syntax: /ticketclear <all/id> <id>", Color.Red);
                        break;
                }
            }
        }

        public static void TicBan(CommandArgs args)
        {
            int numberOfPeopleBanned = 1;
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Syntax: /ticketban <ban/unban/list> <player name/id>", Color.DarkCyan);
                args.Player.SendMessage("/ticketban ban <player name>: stops the player from filing tickets", Color.DarkCyan);
                args.Player.SendMessage("/ticketban unban <player id/name>: unbans a player based on their ID or their name. Use /ticban list to find out banned IDs", Color.DarkCyan);
                args.Player.SendMessage("/ticketban list: lists players that are banned and their IDs", Color.DarkCyan);
                args.Player.SendMessage("Note: /ticketban can be shortened to /ticban", Color.DarkCyan);
            }
            else
            {
                switch (args.Parameters[0].ToLower())
                {
                    case "ban":
                        if (args.Parameters.Count == 1)
                        {
                            args.Player.SendMessage("/ticketban ban <player name>: stops the player from filing tickets", Color.DarkCyan);
                        }
                        else
                        {
                            try
                            {
                                var FindPlayer = TShock.Utils.FindPlayer(args.Parameters[1]);
                                var ListedPlayer = Player.GetPlayerByName(args.Parameters[1]);
                                if (FindPlayer.Count == 1)
                                {
                                    if ((ListedPlayer.GetTicState() == Player.CanSubmitTickets.yes) && (!FindPlayer[0].Group.HasPermission("TicketClear")))
                                    {
                                        ListedPlayer.SetTicState(Player.CanSubmitTickets.no);
                                        args.Player.SendMessage(string.Format("You have revoked the privileges of submitting tickets from {0}", FindPlayer[0].Name), Color.Red);
                                        using (StreamWriter writer = new StreamWriter(@"tshock\bannedfromtics.txt"))
                                        {
                                            writer.WriteLine(FindPlayer[0].Name.ToLower());
                                        }
                                        FindPlayer[0].SendMessage("You can no longer submit tickets.", Color.Red);
                                    }
                                    else if (FindPlayer[0].Group.HasPermission("TicketClear"))
                                    {
                                        args.Player.SendMessage("This player cannot be banned from using tickets.", Color.Red);
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
                            }
                        }
                        break;
                    case "unban":
                        if (args.Parameters.Count == 1)
                        {
                            args.Player.SendMessage("/ticketban unban <player id/name>: unbans a player based on their ID or their name. Use /ticban list to find out banned IDs", Color.DarkCyan);
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

                                    StreamReader reader = new StreamReader(@"tshock\bannedfromtics.txt");
                                    StreamWriter writer = new StreamWriter(@"tshock\tictemp.txt", true);
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
                                    File.Delete(@"tshock\bannedfromtics.txt");
                                    File.Move(@"tshock\tictemp.txt", @"tshock\bannedfromtics.txt");
                                    if (i == 0)
                                    {
                                        args.Player.SendMessage(string.Format("Cannot find player name {0}", args.Parameters[1]), Color.Red);
                                    }
                                    else
                                    {
                                        args.Player.SendMessage(string.Format("You have given back the privileges of submitting tickets to player: {0}. This will take affect when they next log in.", args.Parameters[1]), Color.DarkCyan);
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.WriteLine(string.Format("{0} has unbanned player: {1}", args.Player.Name, args.Parameters[1]));
                                        Console.ResetColor();
                                        i = 0;
                                    }
                                }
                                else
                                {
                                    var file = new List<string>(System.IO.File.ReadAllLines(@"tshock\bannedfromtics.txt"));
                                    file.RemoveAt(id - 1);
                                    File.WriteAllLines(@"tshock\bannedfromtics.txt", file.ToArray());
                                    args.Player.SendMessage(string.Format("You have given back the privileges of submitting tickets to player ID: {0}. This will take affect when they next log in.", args.Parameters[1]), Color.Cyan);
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine(string.Format("{0} has unbanned player ID: {1}", args.Player.Name, args.Parameters[1]));
                                    Console.ResetColor();
                                }
                            }
                            catch (Exception e)
                            {
                                args.Player.SendMessage(string.Format("Cannot find player {0}", args.Parameters[1]), Color.Red);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(e.Message);
                                Console.ResetColor();
                            }
                        }
                        break;
                    case "list":
                        try
                        {
                            StreamReader sr = new StreamReader(@"tshock\bannedfromtics.txt");
                            while (sr.Peek() >= 0)
                            {
                                args.Player.SendMessage(numberOfPeopleBanned + ". " + sr.ReadLine(), Color.Cyan);
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
                        }
                        break;
                    case "help":
                        args.Player.SendMessage("Syntax: /ticketban <ban/unban/list> <player name/id>", Color.DarkCyan);
                        args.Player.SendMessage("/ticketban ban <player name>: stops the player from filing tickets", Color.DarkCyan);
                        args.Player.SendMessage("/ticketban unban <player id/name>: unbans a player based on their ID or their name. Use /ticban list to find out banned IDs", Color.DarkCyan);
                        args.Player.SendMessage("/ticketban list: lists players that are banned and their IDs", Color.DarkCyan);
                        args.Player.SendMessage("Note: /ticketban can be shortened to /ticban", Color.DarkCyan);
                        break;
                    default:
                        args.Player.SendMessage("Syntax: /ticketban <ban/unban/list> <player name/id>", Color.Red);
                        break;
                }
            }
        }
        #endregion
    }
}