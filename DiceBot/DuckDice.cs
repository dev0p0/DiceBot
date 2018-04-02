﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiceBot
{
    class DuckDice:DiceSite
    {
        string accesstoken = "";
        DateTime LastSeedReset = new DateTime();
        public bool ispd = false;
        string username = "";
        long uid = 0;
        DateTime lastupdate = new DateTime();
        HttpClient Client;// = new HttpClient { BaseAddress = new Uri("https://api.primedice.com/api/") };
        HttpClientHandler ClientHandlr;
        public static string[] cCurrencies = new string[] { "BTC","ETH", "LTC", "DOGE","DASH","BCH","XMR" };
        
        
        public DuckDice(cDiceBot Parent)
        {
            _PasswordText = "Api Key: ";
            maxRoll = 99.99m;
            AutoInvest = false;
            AutoWithdraw = false;
            ChangeSeed = true;
            AutoLogin = true;
            BetURL = "https://duckdice.io";
            
            this.Parent = Parent;
            Name = "DuckDice";
            Tip = true;
            TipUsingName = true;
            //Thread tChat = new Thread(GetMessagesThread);
            //tChat.Start();
            SiteURL = "https://duckdice.io/?c=53ea652da4";
            Currencies = cCurrencies;
            Currency = "BTC";
        }

        protected override void CurrencyChanged()
        {
            try
            {
                if (ispd)
                {
                    string sEmitResponse = Client.GetStringAsync("load/" + Currency + "?api_key=" + accesstoken).Result;
                    Quackbalance balance = json.JsonDeserialize<Quackbalance>(sEmitResponse);
                    this.balance = decimal.Parse(balance.user.balance, System.Globalization.NumberFormatInfo.InvariantInfo);
                    Parent.updateBalance(this.balance);
                    sEmitResponse = Client.GetStringAsync("stat/" + Currency + "?api_key=" + accesstoken).Result;
                    QuackStatsDetails Stats = json.JsonDeserialize<QuackStatsDetails>(sEmitResponse);
                    this.profit = decimal.Parse(Stats.profit, System.Globalization.NumberFormatInfo.InvariantInfo);
                    this.wagered = decimal.Parse(Stats.volume, System.Globalization.NumberFormatInfo.InvariantInfo);
                    bets = Stats.bets;
                    wins = Stats.wins;
                    losses = bets - wins;
                    Parent.updateProfit(this.profit);
                    Parent.updateBets(this.bets);
                    Parent.updateLosses(losses);
                    Parent.updateWagered(wagered);
                    Parent.updateWins(wins);
                }
            }
            catch { }
        }

        void GetBalanceThread()
        {
            
            while (ispd)
            {
                if (accesstoken != "" && ((DateTime.Now - lastupdate).TotalSeconds > 60||ForceUpdateStats))
                {
                    try
                    {
                        lastupdate = DateTime.Now;
                        string sEmitResponse = Client.GetStringAsync("load/" + Currency + "?api_key=" + accesstoken).Result;
                        Quackbalance balance = json.JsonDeserialize<Quackbalance>(sEmitResponse);
                        this.balance = decimal.Parse( balance.user.balance, System.Globalization.NumberFormatInfo.InvariantInfo);
                        Parent.updateBalance(this.balance);
                        sEmitResponse = Client.GetStringAsync("stat/" + Currency + "?api_key=" + accesstoken).Result;
                        QuackStatsDetails Stats = json.JsonDeserialize<QuackStatsDetails>(sEmitResponse);
                        this.profit = decimal.Parse(Stats.profit, System.Globalization.NumberFormatInfo.InvariantInfo);
                        this.wagered = decimal.Parse(Stats.volume, System.Globalization.NumberFormatInfo.InvariantInfo);
                        bets = Stats.bets;
                        wins = Stats.wins;
                        losses = bets - wins;
                        Parent.updateProfit(this.profit);
                        Parent.updateBets(this.bets);
                        Parent.updateLosses(losses);
                        Parent.updateWagered(wagered);
                        Parent.updateWins(wins);
                    }
                    catch
                    {

                    }
                        
                }
                Thread.Sleep(1000);
            }
           
        }
        QuackSeed currentseed = null;
        void PlaceBetThreead(object bet)
        {
            PlaceBetObj tmp5 = bet as PlaceBetObj;
            decimal amount = tmp5.Amount;
            decimal chance = tmp5.Chance;
            bool High = tmp5.High;
            StringContent Content = new StringContent(string.Format(System.Globalization.NumberFormatInfo.InvariantInfo, "{{\"amount\":\"{0:0.00000000}\",\"symbol\":\"{1}\",\"chance\":{2:0.00},\"isHigh\":{3}}}", amount, Currency, chance, High ? "true" : "false"), Encoding.UTF8, "application/json");
            try
            {
                string sEmitResponse = Client.PostAsync("play" + "?api_key=" + accesstoken, Content).Result.Content.ReadAsStringAsync().Result;
                QuackBet newbet = json.JsonDeserialize<QuackBet>(sEmitResponse);
                if (newbet.error!=null)
                {
                    Parent.updateStatus(newbet.error);
                    return;
                }
                Bet tmp = new Bet
                {
                    //Id=newbet.ha
                    Amount = decimal.Parse(newbet.bet.betAmount, System.Globalization.NumberFormatInfo.InvariantInfo),
                    Chance = newbet.bet.chance,
                    clientseed = currentseed.clientSeed,
                    Currency = Currency,
                    date = DateTime.Now,
                    high = High,
                    nonce = currentseed.nonce++,
                    Profit = decimal.Parse(newbet.bet.profit, System.Globalization.NumberFormatInfo.InvariantInfo),
                    Roll = newbet.bet.number / 100,
                    serverhash = currentseed.serverSeedHash,
                    Id=newbet.bet.hash,
                    Guid = tmp5.Guid
                };
                lastupdate = DateTime.Now;
                profit = decimal.Parse(newbet.user.profit, System.Globalization.NumberFormatInfo.InvariantInfo);
                wagered = decimal.Parse(newbet.user.volume, System.Globalization.NumberFormatInfo.InvariantInfo);
                balance = decimal.Parse(newbet.user.balance, System.Globalization.NumberFormatInfo.InvariantInfo);
                wins = newbet.user.wins;
                bets = newbet.user.bets;
                losses = bets - wins;
                FinishedBet(tmp);
            }
            catch (Exception e)
            {
                Parent.updateStatus("There was an error placing your bet.");
            }
        }


        protected override void internalPlaceBet(bool High, decimal amount, decimal chance, string Guid)
        {
            new Thread(new ParameterizedThreadStart(PlaceBetThreead)).Start(new PlaceBetObj(High, amount, chance, Guid));
        }
        Random R = new Random();
        public override void ResetSeed()
        {
            try
            {
                string alf = "0123456789qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM";
                string Clientseed = "";
                while (Clientseed.Length < R.Next(15, 25))
                {
                    Clientseed += alf[R.Next(0, alf.Length)];
                }
                StringContent Content = new StringContent(string.Format(System.Globalization.NumberFormatInfo.InvariantInfo, "{{\"clientSeed\":\"{0}\"}}", Clientseed), Encoding.UTF8, "application/json");
                string sEmitResponse = Client.PostAsync("randomize/" + "?api_key=" + accesstoken, Content).Result.Content.ReadAsStringAsync().Result;
                currentseed = json.JsonDeserialize<QuackSeed>(sEmitResponse).current;
            }
            catch (Exception e)
            {

            }
            
        }

        public override void SetClientSeed(string Seed)
        {
            throw new NotImplementedException();
        }

        protected override bool internalWithdraw(decimal Amount, string Address)
        {
            return false;
        }

        public override bool InternalSendTip(string User, decimal amount)
        {
            try
            {
                string cont = string.Format(System.Globalization.NumberFormatInfo.InvariantInfo, "{{\"username\":\"{1}\",\"symbol\":\"{0}\",\"amount\":{2:0.00000000}}}", Currency, User, amount);
                StringContent Content = new StringContent(cont, Encoding.UTF8, "application/json");
                string sEmitResponse = Client.PostAsync("tip-username" + "?api_key=" + accesstoken, Content).Result.Content.ReadAsStringAsync().Result;
                //Parent.DumpLog(sEmitResponse, -1);
                QuackWithdraw tmp = json.JsonDeserialize<QuackWithdraw>(sEmitResponse);
                if (tmp.error == null)
                {
                    return true;
                }

            }
            catch (Exception e)
            {
                Parent.DumpLog(e.ToString(),0);
            }
            return false;
        }
        public override void Donate(decimal Amount)
        {
            SendTip("seuntjie",Amount);
        }
        public override void Login(string Username, string Password, string twofa)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ClientHandlr = new HttpClientHandler { UseCookies = true, AutomaticDecompression= DecompressionMethods.Deflate| DecompressionMethods.GZip, Proxy= this.Prox, UseProxy=Prox!=null };
            Client = new HttpClient(ClientHandlr) { BaseAddress = new Uri("https://duckdice.io/api/") };
            Client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
            Client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
            Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:43.0) Gecko/20100101 Firefox/43.0");
            try
            {
                
                accesstoken =Password;
                //Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",accesstoken);
                accesstoken = Password;
                string sEmitResponse = Client.GetStringAsync("load/"+Currency+"?api_key="+accesstoken).Result;
                        Quackbalance balance = json.JsonDeserialize<Quackbalance>(sEmitResponse);
                        sEmitResponse = Client.GetStringAsync("stat/" + Currency + "?api_key=" + accesstoken).Result;
                        QuackStatsDetails Stats = json.JsonDeserialize<QuackStatsDetails>(sEmitResponse);
                        sEmitResponse = Client.GetStringAsync("randomize" + "?api_key=" + accesstoken).Result;
                        currentseed = json.JsonDeserialize<QuackSeed>(sEmitResponse).current;
                        if (balance!=null && Stats!=null)
                        {
                            this.balance = decimal.Parse(balance.user.balance, System.Globalization.NumberFormatInfo.InvariantInfo);
                            this.profit = decimal.Parse(Stats.profit, System.Globalization.NumberFormatInfo.InvariantInfo);
                            this.wagered = decimal.Parse(Stats.volume, System.Globalization.NumberFormatInfo.InvariantInfo);
                            bets = Stats.bets;
                            wins = Stats.wins;
                            losses = bets - wins;
                            Parent.updateBalance(this.balance);
                            Parent.updateProfit(this.profit);
                            Parent.updateBets(this.bets);
                            Parent.updateLosses(losses);
                            Parent.updateWagered(wagered);
                            Parent.updateWins(wins);
                            
                            ispd = true;
                            lastupdate = DateTime.Now;
                            new Thread(new ThreadStart(GetBalanceThread)).Start();
                            finishedlogin(true);
                            return;
                        }
                    /*}
                }*/
            }
            catch (Exception e)
            {
                finishedlogin(false);
                return;
            }
            finishedlogin(false);
            return;
        }

        public override bool Register(string username, string password)
        {
            throw new NotImplementedException();
        }

        public override bool ReadyToBet()
        {
            return true;
        }

        public override void Disconnect()
        {
            ispd = false;
        }

        public override void GetSeed(long BetID)
        {
            throw new NotImplementedException();
        }

        public override void SendChatMessage(string Message)
        {
            throw new NotImplementedException();
        }

        public virtual decimal GetLucky(string server, string client, int nonce)
        {
            
            return sGetLucky(server,client,nonce);
        }
        public static decimal sGetLucky(string server, string client, int nonce)
        {
            SHA512 betgenerator = SHA512.Create();

            int charstouse = 5;
            
            List<byte> buffer = new List<byte>();
            string msg = server+ client + nonce.ToString();
            foreach (char c in msg)
            {
                buffer.Add(Convert.ToByte(c));
            }

            byte[] hash = betgenerator.ComputeHash(buffer.ToArray());

            StringBuilder hex = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                hex.AppendFormat("{0:x2}", b);


            for (int i = 0; i < hex.Length; i += charstouse)
            {

                string s = hex.ToString().Substring(i, charstouse);

                decimal lucky = int.Parse(s, System.Globalization.NumberStyles.HexNumber);
                if (lucky < 1000000)
                {
                    decimal tmp = (lucky % 10000)/100m;
                    return tmp;
                }
            }
            return 0;
        }

    }
    public class QuackLogin
    {
        public string token { get; set; }
    }
    public class Quackbalance
    {
        public QuackStats user { get; set; }
        public string hash { get; set; }
        public string username { get; set; }
        public string balance { get; set; }
        public QuackStats session { get; set; }
    }
    public class QuackStats
    {
        
        public QuackStats user { get; set; }
        public string hash { get; set; }
        public string username { get; set; }
        public string balance { get; set; }
        public QuackStats session { get; set; }
        public int bets { get; set; }
        public int wins { get; set; }
        public string volume { get; set; }
        public string profit { get; set; }
        
    }
    public class QuackStatsDetails
    {
        public int bets { get; set; }
        public int wins { get; set; }
        public string profit { get; set; }
        public string volume { get; set; }
    }
    public class QuackBet
    {
        public string error { get; set; }
        public QuackBet bet { get; set; }
        public QuackStats user { get; set; }
        public string hash { get; set; }
        public string symbol { get; set; }
        public bool result { get; set; }
        public bool isHigh { get; set; }
        public decimal number { get; set; }
        public decimal threshold { get; set; }
        public decimal chance { get; set; }
        public decimal payout { get; set; }
        public string betAmount { get; set; }
        public string winAmount { get; set; }
        public string profit { get; set; }
        public long nonce { get; set; }
       
    }
    public class QuackSeed
    {
        public QuackSeed current { get; set; }
        public string clientSeed { get; set; }
        public long nonce { get; set; }
        public string serverSeedHash { get; set; }
    }
    public class QuackWithdraw
    {
        public string error { get; set; }
        
    }
}
