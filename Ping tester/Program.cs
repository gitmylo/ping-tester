using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace Ping_tester
{
    internal class Program
    {
        public static int highestId = 0;
        public static int logLevel = 0;
        public bool keepRunning;
        public static void Main(string[] args)
        {
            logLevel = askOption("What level of logs do you want?\n(0) All logs\n(1) No warnings/info\n(2) Only success");
            Console.Clear();
            int mode = askOption("Which type of scan do you want to do?\n(0) Ip scan\n(1) Port scan an ip file");
            Console.Clear();
            switch (mode)
            {
                case 0:
                    ipScanner();
                    break;
                case 1:
                    portScanner();
                    break;
                default:
                    PrintInfo("Not a valid option", messageType.Error);
                    break;
            }
            Console.ReadLine();
        }

        public static void portScanner()
        {
            string[] files = Directory.GetDirectories("out/ips/");
            string askMsg = "Which files do you want to use ips from\n";
            int i = 0;
            foreach (string path in files) askMsg += $"({i++}) {path}\n";
            int chosen = askOption(askMsg);
            if (chosen < files.Length)
            {
                string chosenPath = files[chosen];
                string allips = "";
                PrintInfo("Adding contents of all files...", messageType.Info);
                foreach (string file in Directory.GetFiles(chosenPath))
                {
                    allips += File.ReadAllText(file);
                }
                string[] ipslist = allips.Split('\n');
                int currentIp = 0;
                PrintInfo("Combined all files", messageType.Info);
                int port = askOption("Which port to scan?");
                int threads = askOption("How many threads?");
                int timeMs = askOption("Timeout in milliseconds?");
                Console.Clear();
                DateTime now = DateTime.Now;
                String fileName = $"{now.Year}-{now.DayOfYear} ({now.Hour}.{now.Minute}.{now.Second})";
                if (!Directory.Exists($"out/ports/{fileName}")) Directory.CreateDirectory($"out/ports/{fileName}");
                StreamWriter writer = File.CreateText($"out/ports/{fileName}/{port}.txt");
                PrintInfo("Starting port scans", messageType.Info);
                for (int j = 0; j < threads; j++)
                {
                    PrintInfo($"Thread {j} created", messageType.Info);
                    Thread.Sleep(5);
                    new Thread(() =>
                    {
                        int threadNum = j;
                        bool done = false;
                        while (!done)
                        {
                            if (currentIp < ipslist.Length)
                            {
                                String thisIp = ipslist[currentIp++];
                                thisIp = thisIp.TrimEnd('\n', '\0', '\r', ' ');
                                PrintInfo($"Testing {thisIp}:{port}", messageType.Info);
                                
                                if (portChecker.IsPortOpen(thisIp, port, TimeSpan.FromMilliseconds(timeMs)))
                                {
                                    PrintInfo($"Port {port} open on {thisIp}:{port}", messageType.Success);
                                    writer.WriteLine($"{thisIp}:{port}");
                                    writer.Close();
                                    writer.Dispose();
                                    writer = File.AppendText($"out/ports/{fileName}/{port}.txt");
                                }
                                Thread.Sleep(5);
                            }
                            else done = true;
                        }
                        PrintInfo($"Thread {threadNum} ended", messageType.Info);
                    }).Start();
                    
                }
                Console.WriteLine("Created all threads!");
            }
            else
            {
                PrintInfo("Not a valid file", messageType.Error);
            }
        }

        public static void ipScanner()
        {
            int AttackType = askOption("What type of attack?\n(0) Root From Single ip\n(1) Random ips");
            string target = "";
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
            
            int timeMs = askOption("Timeout in milliseconds?");
            Console.Clear();

            DateTime now = DateTime.Now;
            string dirName = $"{now.Year}-{now.DayOfYear} ({now.Hour}.{now.Minute}.{now.Second})";
            for (int i = 0; i < Threads; i++)
            {
                Thread.Sleep(5);
                new Thread(() =>
                {
                    target = AttackType == 0?target:generateIp(highestId);
                    pinger ping = new pinger(target, highestId++, dirName, AttackType == 1, timeMs);
                }).Start();
            }
        }

        public static int askOption(String question)
        {
            bool success = false;
            int outNum = 0;
            while (!success)
            {
                Console.WriteLine(question);
                string answer = Console.ReadLine();
                success = int.TryParse(answer, out outNum);
            }

            return outNum;
        }

        public enum messageType
        {
            Info, Warning, Error, Success
        }

        public static string generateIp(int id)
        {
            Random r = new Random(Environment.TickCount + id);
            return $"{r.Next(239)}.{r.Next(255)}.{r.Next(255)}.{r.Next(255)}";
        }
        public static void PrintInfo(String message, messageType type)
        {
            if ((type == messageType.Warning || type == messageType.Info) && logLevel >= 1) return;
            if (type == messageType.Error && logLevel >= 2) return;
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
            Console.ForegroundColor = ConsoleColor.White;
        }
    }

    internal class portChecker
    {
        public static bool IsPortOpen(string host, int port, TimeSpan timeout)
        {
            try
            {
                using(var client = new TcpClient())
                {
                    var result = client.ConnectAsync(host, port);
                    var success = result.Wait(timeout);
                    client.Close();
                    return success;
                }
            }
            catch(Exception e)
            {
                Program.PrintInfo(e.Message, Program.messageType.Error);
                return false;
            }
        }
    }

    internal class pinger
    {
        public static List<String> rootedIps = new List<string>();
        public int id;
        public String fileName;
        public bool continueAfter;
        public StreamWriter writer;
        public int delayMs;

        private Ping p = new Ping();
        public pinger(String ip, int id, String fileName, bool continueAfter, int delayBetweenMs)
        {
            try
            {
                bool flagged = (ip.StartsWith("0.") || ip.StartsWith("10.") || ip.StartsWith("127.") || ip.StartsWith("169.254.") || ip.StartsWith("255.255.255.255"));//flag all these local ips
                if (flagged)
                {
                    Program.PrintInfo("Invalid ip given", Program.messageType.Error);
                    throw new Exception("Invalid ip given");
                }
                this.id = id;
                this.fileName = fileName;
                this.continueAfter = continueAfter;
                this.delayMs = delayBetweenMs;
                p.PingCompleted += POnPingCompleted;
                Program.PrintInfo($"Pinging {ip}...", Program.messageType.Info);
                Thread.Sleep(2); 
                p.SendPingAsync(ip);
            }
            catch (Exception e)
            {
                if (continueAfter) new pinger(Program.generateIp(id), id, fileName, true, delayBetweenMs);
            }
            
        }

        public pinger(String ip, int id, String fileName, bool continueAfter, int delayBetweenMs, StreamWriter writer) : this(ip, id, fileName, continueAfter, delayBetweenMs)
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
                    if (!Directory.Exists($"out/ips/{fileName}")) Directory.CreateDirectory($"out/ips/{fileName}");
                    using (StreamWriter writer = File.CreateText($"out/ips/{fileName}/{id}.txt"))
                    {
                        for (int i = 0; i < 256; i++)
                        {
                            for (int j = 0; j < 256; j++)
                            {
                                Thread.Sleep(delayMs);
                                //new pinger(ipWithoutLast+i);
                                new pinger(ipWithoutLast + i + "." + j, id, fileName, false, delayMs, writer);
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
            
            if (continueAfter) new pinger(Program.generateIp(id), Program.highestId++, fileName, true, delayMs);
        }
    }
}