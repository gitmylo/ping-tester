using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;

namespace Ping_tester
{
    internal class Program
    {
        public static int highestId = 0;
        public static void Main(string[] args)
        {
            int AttackType = askOption("What type of attack?\n(0) Root From Single ip\n(1) Random ips");
            String target = "";
            int Threads;
            if (AttackType == 0)
            {
                Threads = 1;
                Console.WriteLine("Which ip do you want to root from (if online)");
                target = Console.ReadLine();
            }
            else
            {
                Threads = askOption("How many threads?");
            }

            DateTime now = DateTime.Now;
            String dirName = $"{now.Year}-{now.DayOfYear} ({now.Hour}.{now.Minute}.{now.Second})";
            for (int i = 0; i < Threads; i++)
            {
                Thread.Sleep(5);
                new Thread(() =>
                {
                    target = AttackType == 0?target:generateIp(highestId);
                    pinger ping = new pinger(target, highestId++, dirName, AttackType == 1);
                }).Start();
            }
            Console.ReadLine();
        }

        public static int askOption(String question)
        {
            bool success = false;
            int outNum = 0;
            while (!success)
            {
                Console.WriteLine(question);
                String answer = Console.ReadLine();
                success = int.TryParse(answer, out outNum);
            }

            return outNum;
        }

        public enum messageType
        {
            Info, Warning, Error, Success
        }

        public static String generateIp(int id)
        {
            Random r = new Random(Environment.TickCount + id);
            return $"{r.Next(255)}.{r.Next(255)}.{r.Next(255)}.{r.Next(255)}";
        }
        public static void PrintInfo(String message, messageType type)
        {
            String messageGenned = "";
            switch (type)
            {
                case messageType.Info:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    messageGenned = ("[i] ");
                    break;
                case messageType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    messageGenned = ("[!] ");
                    break;
                case messageType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    messageGenned = ("[X] ");
                    break;
                case messageType.Success:
                    Console.ForegroundColor = ConsoleColor.Green;
                    messageGenned = ("[V] ");
                    break;
            }

            messageGenned += message;
            Console.WriteLine(messageGenned);
        }
    }

    internal class pinger
    {
        public static List<String> rootedIps = new List<string>();
        public int id;
        public String fileName;
        public bool continueAfter;
        public StreamWriter writer;

        private Ping p = new Ping();
        public pinger(String ip, int id, String fileName, bool continueAfter)
        {
            try
            {
                this.id = id;
                this.fileName = fileName;
                this.continueAfter = continueAfter;
                p.PingCompleted += POnPingCompleted;
                Program.PrintInfo($"Pinging {ip}...", Program.messageType.Info);
                Thread.Sleep(2); 
                p.SendPingAsync(ip);
            }
            catch (Exception e)
            {
                if (continueAfter) new pinger(Program.generateIp(id), id, fileName, true);
            }
            
        }

        public pinger(String ip, int id, String fileName, bool continueAfter, StreamWriter writer) : this(ip, id, fileName, continueAfter)
        {
            this.writer = writer;
        }

        private void POnPingCompleted(object sender, PingCompletedEventArgs e)
        {
            switch (e.Reply.Status)
            {
                case IPStatus.Success:
                    String ip = e.Reply.Address.ToString();
                    if (writer != null) writer.WriteLine(ip);
                    
                    String[] ipSegs = ip.Split('.');
                    //String ipWithoutLast = ip.Substring(0, ip.Length - ipSegs[ipSegs.Length - 1].Length);
                    String ipWithoutLast = ip.Substring(0, ip.Length - ipSegs[ipSegs.Length - 1].Length - ipSegs[ipSegs.Length - 2].Length - 1);
                    
                    bool canRoot = !rootedIps.Contains(ipWithoutLast);
                    Program.PrintInfo($"Got reply from {e.Reply.Address}" + (canRoot?" Rooting this ip!" : "!"), Program.messageType.Success);
                    if (!canRoot) break;
                    rootedIps.Add(ipWithoutLast);
                    if (!Directory.Exists($"out/{fileName}")) Directory.CreateDirectory($"out/{fileName}");
                    using (StreamWriter writer = File.CreateText($"out/{fileName}/{id}.txt"))
                    {
                        for (int i = 0; i < 256; i++)
                        {
                            for (int j = 0; j < 256; j++)
                            {
                                //new pinger(ipWithoutLast+i);
                                new pinger(ipWithoutLast + i + "." + j, id, fileName, false, writer);
                            }
                        }
                    }
                    break;
                case IPStatus.Unknown:
                    Program.PrintInfo($"Failed to ping {e.Reply.Address} for unknown reason.", Program.messageType.Error);
                    break;
                case IPStatus.DestinationProhibited:
                    Program.PrintInfo($"Failed to ping {e.Reply.Address} but host is up.", Program.messageType.Warning);
                    break;
                case IPStatus.BadDestination:
                    Program.PrintInfo($"Failed to ping {e.Reply.Address} because host is down", Program.messageType.Error);
                    break;
                default:
                    //Program.PrintInfo($"Failed to ping {e.Reply.Address} for other reason.", Program.messageType.Error);
                    break;
            }
            
            if (continueAfter) new pinger(Program.generateIp(id), Program.highestId++, fileName, true);
        }
    }
}