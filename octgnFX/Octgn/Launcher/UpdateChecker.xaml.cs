﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Xml;
using Octgn.Data;
using Octgn.Definitions;
using Octgn.Scripting;
using Skylabs.Lobby.Threading;

namespace Octgn.Launcher
{
    /// <summary>
    ///   Interaction logic for UpdateChecker.xaml
    /// </summary>
    public partial class UpdateChecker
    {
        private readonly List<string> _errors = new List<string>();
        // private bool stopReading; // not used

        public UpdateChecker()
        {
            IsClosingDown = false;
            InitializeComponent();
            LazyAsync.Invoke(VerifyAllDefs);
            lblStatus.Content = "";
            //Thread t = new Thread(VerifyAllDefs);
            //t.Start();
        }

        public static bool CheckGameDef(GameDef game)
        {
            Program.Game = new Game(game);
            Program.Game.TestBegin();
            var engine = new Engine(true);
            string[] terr = engine.TestScripts(Program.Game);
            Program.Game.End();
            if (terr.Length > 0)
            {
                String ewe = terr.Aggregate("",
                                            (current, s) =>
                                            current +
                                            (s + Environment.NewLine));
                var er = new ErrorWindow(ewe);
                er.ShowDialog();
            }
            return terr.Length == 0;
        }

        public bool IsClosingDown { get; set; }

        private void CheckForUpdates()
        {
            try
            {
                string[] update = ReadUpdateXml("https://raw.github.com/kellyelton/Octgn/master/currentversion.xml");


                Assembly assembly = Assembly.GetExecutingAssembly();
                Version local = assembly.GetName().Version;
                var online = new Version(update[0]);
                bool isupdate = online > local;
                string ustring = update[1];
                Dispatcher.BeginInvoke(new Action<bool, string>(UpdateCheckDone), isupdate, ustring);
            }
            catch (Exception)
            {
                Dispatcher.BeginInvoke(new Action<bool, string>(UpdateCheckDone), false, "");
            }
        }

        private void UpdateCheckDone(bool result, string url)
        {
            if (result)
            {
                IsClosingDown = true;
                switch (
                    MessageBox.Show("An update is available. Would you like to download now?", "Update Available",
                                    MessageBoxButton.YesNo, MessageBoxImage.Question))
                {
                    case MessageBoxResult.Yes:
                        Process.Start(url);
                        break;
                }
            }
            Close();
        }

        private void VerifyAllDefs()
        {
            UpdateStatus("Loading Game Definitions...");
            try
            {
                if (Program.GamesRepository == null)
                    Program.GamesRepository = new GamesRepository();
                var g2R = new List<Data.Game>();
                using (MD5 md5 = new MD5CryptoServiceProvider())
                {
                    foreach (Data.Game g in Program.GamesRepository.Games)
                    {
                        string fhash = "";

                        UpdateStatus("Checking Game: " + g.Name);

                        if (!File.Exists(g.Filename))
                        {
                            _errors.Add("[" + g.Name + "]: Def file doesn't exist at " + g.Filename);
                            continue;
                        }
                        using (var file = new FileStream(g.Filename, FileMode.Open))
                        {
                            byte[] retVal = md5.ComputeHash(file);
                            fhash = BitConverter.ToString(retVal).Replace("-", ""); // hex string
                        }
                        if (fhash.ToLower() == g.FileHash.ToLower()) continue;

                        Program.Game = new Game(GameDef.FromO8G(g.Filename));
                        Program.Game.TestBegin();
                        //IEnumerable<Player> plz = Player.All;
                        var engine = new Engine(true);
                        string[] terr = engine.TestScripts(Program.Game);
                        Program.Game.End();
                        if (terr.Length <= 0)
                        {
                            Program.GamesRepository.UpdateGameHash(g,fhash);
                            continue;
                        }
                        _errors.AddRange(terr);
                        g2R.Add(g);
                    }
                }
                foreach (Data.Game g in g2R)
                    Program.GamesRepository.Games.Remove(g);
                if (_errors.Count > 0)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                                                          {
                                                              String ewe = _errors.Aggregate("",
                                                                                             (current, s) =>
                                                                                             current +
                                                                                             (s + Environment.NewLine));
                                                              var er = new ErrorWindow(ewe);
                                                              er.ShowDialog();
                                                          }));
                }
                UpdateStatus("Checking for updates...");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                if (Debugger.IsAttached) Debugger.Break();
            }
            CheckForUpdates();
        }

        private void UpdateStatus(string stat)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                                                  {
                                                      try
                                                      {
                                                          lblStatus.Content = stat;
                                                      }
                                                      catch (Exception)
                                                      {
                                                          Debugger.Break();
                                                      }
                                                  }));
        }

/*
        private bool FileExists(string url)
        {
            bool result;
            using (var client = new WebClient())
            {
                try
                {
                    Stream str = client.OpenRead(url);
                    result = str != null;
                }
                catch
                {
                    result = false;
                }
            }
            return result;
        }
*/

        private static string[] ReadUpdateXml(string url)
        {
            var values = new string[2];
            try
            {
                WebRequest wr = WebRequest.Create(url);
                wr.Timeout = 15000;
                WebResponse resp = wr.GetResponse();
                Stream rgrp = resp.GetResponseStream();
                if (rgrp != null)
                    using (XmlReader reader = XmlReader.Create(rgrp))
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsStartElement()) continue;
                            if (reader.IsEmptyElement) continue;
                            switch (reader.Name)
                            {
                                case "Version":
                                    values = new string[2];
                                    if (reader.Read())
                                    {
                                        values[0] = reader.Value;
                                    }
                                    break;
                                case "Location":
                                    if (reader.Read())
                                    {
                                        values[1] = reader.Value;
                                    }
                                    break;
                            }
                        }
                    }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                if (Debugger.IsAttached) Debugger.Break();
            }
            return values;
        }
    }
}