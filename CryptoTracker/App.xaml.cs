﻿using CryptoTracker.Helpers;
using CryptoTracker.Views;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Telerik.UI.Xaml.Controls.Chart;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CryptoTracker {
	sealed partial class App : Application {
        
        internal static string coin       = "EUR";
        internal static string coinSymbol = "€";

        internal static int pivotIndex = 0;

        internal static List<JSONcoins> coinList = new List<JSONcoins>();
        internal static List<string> pinnedCoins;
        internal static List<JSONhistoric> historic = new List<JSONhistoric>();
        internal static JSONstats stats = new JSONstats();
        internal static List<JSONexchanges> exchanges = new List<JSONexchanges>();

        internal static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public App() {

            try {
                string _theme  = localSettings.Values["Theme"].ToString();
                string _coin   = localSettings.Values["Coin"].ToString();
                string _pinned = localSettings.Values["Pinned"].ToString();
                pinnedCoins = new List<string>(_pinned.Split( new char[] { '|' } ));
                pinnedCoins.Remove("");

                if (_theme != null) {
                    switch (_theme) {
                        case "Light":
                            RequestedTheme = ApplicationTheme.Light;
                            break;
                        case "Dark":
                            RequestedTheme = ApplicationTheme.Dark;
                            break;
                    }
                }
                if (_coin != null) {
                    coin = _coin;
                    switch (coin) {
                        case "EUR":
                            coinSymbol = "€";
                            break;
                        case "GBP":
                            coinSymbol = "£";
                            break;
                        case "USD":
                        case "CAD":
                        case "AUD":
                        case "MXN":
                            coinSymbol = "$";
                            break;
                        case "CNY":
                        case "JPY":
                            coinSymbol = "¥";
                            break;
                        case "INR":
                            coinSymbol = "₹";
                            break;
                    }
                }
                
            } catch (Exception ex){
                // Default: Windows theme, EUR and {BTC, ETH, LTC and XRP}
                localSettings.Values["Theme"] = "Windows";
                localSettings.Values["Coin"] = "EUR";
                localSettings.Values["Pinned"] = "BTC|ETH|LTC|XRP";
                this.RequestedTheme = (new UISettings().GetColorValue(UIColorType.Background) == Colors.Black) ? ApplicationTheme.Dark : ApplicationTheme.Light;
                pinnedCoins = new List<string>(new string[] {"BTC", "ETH", "LTC", "XRP"});
            }

            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.UnhandledException += OnUnhandledException;
        }
        // #########################################################################################
        protected override void OnLaunched(LaunchActivatedEventArgs e) {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null) {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated) {
                    //TODO: Cargar el estado de la aplicación suspendida previamente
                }

                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false) {
                if (rootFrame.Content == null) {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(900, 550));
                Window.Current.Activate();
            }
#if !DEBUG
            AppCenter.Start("37e61258-8639-47d6-9f6a-d47d54cd8ad5", typeof(Analytics), typeof(Crashes));
#endif
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e) {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
        private void OnSuspending(object sender, SuspendingEventArgs e) {
            var deferral = e.SuspendingOperation.GetDeferral();

            deferral.Complete();
        }
        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e) {
            Analytics.TrackEvent("UNHANDLED: " + e.Message);
        }

        // ###############################################################################################
        internal static void UpdatePinnedCoins() {
            if (App.pinnedCoins.Count > 0) { 
                string s = "";
                foreach (var item in App.pinnedCoins) {
                    s += item + "|";
                }
                s = s.Remove(s.Length - 1);
                App.localSettings.Values["Pinned"] = s;
            }
        }



        // ###############################################################################################
        //  (GET) current price of a coin
        internal static double GetCurrentPrice(string crypto, string market) {
            string URL = "https://min-api.cryptocompare.com/data/price?fsym=" + crypto + "&tsyms=" + App.coin;
            // + "&api_key=569e637087fe54f3c739de6f8618187f805fb0a5f662f9179add6c027809c286";

            if (market != "defaultMarket") 
                URL += "&e=" + market;

            Uri uri = new Uri(URL);

            try {
                var data = GetJSONAsync(uri).Result;

                if((float)data[coin] > 99)
                    return Math.Round((float)data[coin], 2);
                else {
                    return Math.Round((float)data[coin], 4);
                }
                
            } catch (Exception) {
                return 0;
            }
        }

        // ###############################################################################################
        //  (GET) list of coins
        internal async static Task GetCoinList() {
            string URL = "https://min-api.cryptocompare.com/data/top/totalvol?limit=100&tsym=" + coin
                + "&api_key=569e637087fe54f3c739de6f8618187f805fb0a5f662f9179add6c027809c286";
            Uri uri = new Uri(URL);

            try {
                var data = await GetJSONAsync(uri);
                JSONcoins.HandleJSON(data);

                // Get coins ranked 101-200
                uri = new Uri(URL + "&page=1");
                data = await GetJSONAsync(uri);
                JSONcoins.HandleJSON(data);

                // Get coins ranked 201-300
                uri = new Uri(URL + "&page=2");
                data = await GetJSONAsync(uri);
                JSONcoins.HandleJSON(data);

            } catch (Exception ex) {
                await new MessageDialog(ex.Message).ShowAsync();
            }
        }

        // ###############################################################################################
        //  (GET) info about a coin
        internal async static Task<JSONsnapshot> GetCoinInfo(int id) {
            string URL = "https://www.cryptocompare.com/api/data/coinsnapshotfullbyid/?id=" + id;
            Uri uri = new Uri(URL);

            try {
                var data = await GetJSONAsync(uri);
                JSONsnapshot snapshot = JSONsnapshot.HandleJSON(data);
                return snapshot;

            } catch (Exception ex) {
                await new MessageDialog(ex.Message).ShowAsync();
                return null;
            }
        }

        // ###############################################################################################
        //  (GET) Historic prices
        internal async static Task GetHisto(string crypto, string time, int limit) {
            if (crypto == "MIOTA")
                crypto = "IOT";

            //CCCAGG Bitstamp Bitfinex Coinbase HitBTC Kraken Poloniex 
            string URL = "https://min-api.cryptocompare.com/data/histo" + time + "?e=CCCAGG&fsym="
                + crypto + "&tsym=" + coin + "&limit=" + limit;

            if (limit == 0)
                URL = "https://min-api.cryptocompare.com/data/histoday?e=CCCAGG&fsym=" + crypto + "&tsym=" + coin + "&allData=true";

            Uri uri = new Uri(URL);
            HttpClient httpClient = new HttpClient();

            try {
                string response = await httpClient.GetStringAsync(uri);
                var data = JToken.Parse(response);

                App.historic.Clear();
                App.historic = JSONhistoric.HandleHistoricJSON(data);
                

            } catch (Exception) {
                App.historic.Clear();
                App.historic = JSONhistoric.HandleHistoricJSONnull(limit);
                //var dontWait = await new MessageDialog(ex.Message).ShowAsync();
            }
            finally{
                httpClient.Dispose();
            }
        }

        internal async static Task<List<JSONhistoric>> GetHistoricalPrices(string crypto, string timeSpan) {
            if (crypto == "MIOTA")
                crypto = "IOT";

            var tuple = ParseTimeSpan(timeSpan);
            timeSpan = tuple.Item1;
            int limit = tuple.Item2;

            //CCCAGG Bitstamp Bitfinex Coinbase HitBTC Kraken Poloniex 
            string URL = "https://min-api.cryptocompare.com/data/histo" + timeSpan + "?e=CCCAGG&fsym="
                + crypto + "&tsym=" + coin + "&limit=" + limit;

            if (limit == 0)
                URL = "https://min-api.cryptocompare.com/data/histoday?e=CCCAGG&fsym=" + crypto + "&tsym=" + coin + "&allData=true";

            Uri uri = new Uri(URL);
            HttpClient httpClient = new HttpClient();

            try {
                string response = await httpClient.GetStringAsync(uri);
                var data = JToken.Parse(response);

                return JSONhistoric.HandleHistoricJSON(data);
            }
            catch (Exception) {
                return JSONhistoric.HandleHistoricJSONnull(limit);
            }
            finally {
                httpClient.Dispose();
            }
        }

        internal static Tuple<string, int> ParseTimeSpan(string timeSpan) {
            switch (timeSpan) {
                case "hour":    return Tuple.Create("minute", 60);
                case "day":     return Tuple.Create("minute", 1500);
                case "week":    return Tuple.Create("hour", 168);
                case "month":   return Tuple.Create("hour", 744);
                case "year":    return Tuple.Create("day", 365);
                case "all":     return Tuple.Create("day", 0);
                default:        return Tuple.Create("day", 7);
            }
        }

        // ###############################################################################################
        //  (GET) coin stats
        internal async static Task GetCoinStats(string crypto, string market) {

            string URL = "https://min-api.cryptocompare.com/data/pricemultifull?fsyms=" + crypto + "&tsyms=" + coin;
            
            if(market != "defaultMarket") 
                URL += "&e=" + market;

            Uri uri = new Uri(URL);
            HttpClient httpClient = new HttpClient();

            try {
                string response = await httpClient.GetStringAsync(uri);
                var data = JToken.Parse(response);

                stats = JSONstats.HandleStatsJSON(data, crypto, coin);

            }
            catch (Exception ex) {
                await new MessageDialog(ex.Message).ShowAsync();
            }
            finally {
                httpClient.Dispose();
            }
        }

        // ###############################################################################################
        //  (GET) top exchanges
        internal async static Task GetTopExchanges(string crypto, string toSym) {

            String URL = "https://min-api.cryptocompare.com/data/top/exchanges?fsym=" + crypto  +"&tsym="+ toSym + "&limit=8";

            Uri uri = new Uri(URL);
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage httpResponse = new HttpResponseMessage();

            String response = "";

            try {
                httpResponse = await httpClient.GetAsync(uri);
                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadAsStringAsync();
                var data = JToken.Parse(response);

                exchanges.Clear();
                int lastIndex = ((JContainer)data["Data"]).Count;
                for (int i = 0; i < lastIndex; i++) {
                    exchanges.Add(JSONexchanges.GetExchanges(data["Data"][i]));
                }

            } 
            catch (Exception ex) {
                await new MessageDialog(ex.Message).ShowAsync();
            }
            finally {
                httpResponse.Dispose();
                httpClient.Dispose();
            }
        }

        // ###############################################################################################
        //  (GET) coin description
        internal static async Task<string> GetCoinDescription(string crypto, int lines = 5) {
            String URL = string.Format("https://krausefx.github.io/crypto-summaries/coins/{0}-{1}.txt", crypto.ToLower(), lines);
            Uri uri = new Uri(URL);

            try {
                string data = await GetStringAsync(uri);
                return data;

            } catch (Exception) {
                return "No description found for this coin.";
            }
        }

        // ###############################################################################################
        //  (GET) top 100 coins (by marketcap)
        internal async static Task<ObservableCollection<Top100coin>> GetTop100() {
            int limit = 100;
            String URL = string.Format("https://min-api.cryptocompare.com/data/top/totalvolfull?tsym={0}&limit={1}", coin, limit);

            Uri uri = new Uri(URL);
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            httpResponse.Headers.Add("X-CMC_PRO_API_KEY", "569e637087fe54f3c739de6f8618187f805fb0a5f662f9179add6c027809c286");

            String response = "";

            try {
                httpResponse = await httpClient.GetAsync(uri);
                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadAsStringAsync();
                var data = JToken.Parse(response);
                data = data["Data"];

                var coinn = ((JProperty)data[0]["RAW"].First).Name;
                ObservableCollection<Top100coin> topCoins = new ObservableCollection<Top100coin>();

                for (int i = 0; i < limit; i++) {
                    var symbol = data[i]["CoinInfo"]["Name"].ToString();

                    // There are some coins without RAW data
                    if (data[i]["RAW"] == null){
                        topCoins.Add(
                           new Top100coin {
                               Name = data[i]["CoinInfo"]["FullName"].ToString() ?? "NULL",
                               Symbol = symbol,
                               Src = string.Format("/Assets/Icons/icon{0}.png", symbol),
                               FavIcon = pinnedCoins.Contains(symbol) ? "\uEB52" : "\uEB51"});
                    }
                    else {
                        var change = Math.Round((float)(data[i]["RAW"][coinn]["CHANGEPCT24HOUR"] ?? 0), 3);
                        topCoins.Add(
                            new Top100coin {
                                Rank = (i + 1).ToString(),
                                Name = data[i]["CoinInfo"]["FullName"].ToString() ?? "NULL",
                                Symbol = symbol,
                                Price = ToKMB((double)(data[i]["RAW"][coinn]["PRICE"] ?? "0")) + coinSymbol,
                                Vol24 = ToKMB((double)(data[i]["RAW"][coinn]["TOTALVOLUME24HTO"] ?? "0")) + coinSymbol,
                                MarketCap = ToKMB((double)(data[i]["RAW"][coinn]["MKTCAP"] ?? "0")) + coinSymbol,
                                Change24h = change.ToString() + "%",
                                ChangeFG = change < 0 ? (SolidColorBrush)Current.Resources["pastelRed"] : (SolidColorBrush)Current.Resources["pastelGreen"],
                                Src = string.Format("/Assets/Icons/icon{0}.png", symbol),
                                FavIcon = pinnedCoins.Contains(symbol) ? "\uEB52" : "\uEB51"
                            });
                    }
                    
                }

                return topCoins;

            } catch (Exception ex) {
                await new MessageDialog(ex.Message).ShowAsync();
                return new ObservableCollection<Top100coin>();
            }
        }

        // ###############################################################################################
        //  (GET) global stats
        internal async static Task<GlobalStats> GetGlobalStats() {
            String URL = "https://api.coingecko.com/api/v3/global";

            Uri uri = new Uri(URL);
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage httpResponse = new HttpResponseMessage();

            String response = "";

            try {
                httpResponse = await httpClient.GetAsync(uri);
                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadAsStringAsync();
                var data = JToken.Parse(response);
                data = data["data"];

                GlobalStats g = new GlobalStats();
                g.ActiveCurrencies  = data["active_cryptocurrencies"].ToString();
                g.BtcDominance      = Math.Round((double)data["market_cap_percentage"]["btc"], 2).ToString() + "%";
                g.TotalVolume       = ToKMB((double)(data["total_volume"][coin.ToLower()] ?? data["total_volume"]["usd"])) + coinSymbol;
                g.TotalMarketCap    = ToKMB((double)(data["total_market_cap"][coin.ToLower()] ?? data["total_market_cap"]["usd"])) + coinSymbol;
                return g;

            } catch (Exception ex) {
                //await new MessageDialog(ex.Message).ShowAsync();
                return new GlobalStats();
            }
        }

        public static string ToKMB(double num) {
            if (num > 999999999) {
                return num.ToString("0,,,.##B", CultureInfo.InvariantCulture);
            } else
            if (num > 999999) {
                return num.ToString("0,,.##M", CultureInfo.InvariantCulture);
            } else
            if (num > 999) {
                num = Math.Round(num, 2);
                return num.ToString(CultureInfo.InvariantCulture);
            } else {
                num = Math.Round(num, 3);
                return num.ToString(CultureInfo.InvariantCulture);
            }
        }

        // ###############################################################################################
        /// <summary>
        /// do NOT mess with async methods...
        /// 
        /// Thank god I found this article (thanks Stephen Cleary)
        /// http://blog.stephencleary.com/2012/07/dont-block-on-async-code.html
        /// 
        /// </summary>
        private static async Task<JToken> GetJSONAsync(Uri uri) {

            using (var client = new HttpClient()) {
                var jsonString = await client.GetStringAsync(uri).ConfigureAwait(false);
                return JToken.Parse(jsonString);
            }
        }
        private static async Task<string> GetStringAsync(Uri uri) {
            using (var client = new HttpClient()) {
                var s = await client.GetStringAsync(uri).ConfigureAwait(false);
                return s;
            }
        }

        // ###############################################################################################
        //  Adjust axis
        internal static DateTimeContinuousAxis AdjustAxis(DateTimeContinuousAxis DateTimeAxis, string timeSpan) {
            switch (timeSpan) {
                case "hour":
                    DateTimeAxis.LabelFormat = "{0:HH:mm}";
                    DateTimeAxis.MajorStepUnit = Telerik.Charting.TimeInterval.Minute;
                    DateTimeAxis.MajorStep = 10;
                    DateTimeAxis.Minimum = DateTime.Now.AddHours(-1);
                    break;

                case "day":
                    DateTimeAxis.LabelFormat = "{0:HH:mm}";
                    DateTimeAxis.MajorStepUnit = Telerik.Charting.TimeInterval.Hour;
                    DateTimeAxis.MajorStep = 6;
                    DateTimeAxis.Minimum = DateTime.Now.AddDays(-1);
                    break;

                case "week":
                    DateTimeAxis.LabelFormat = "{0:ddd d}";
                    DateTimeAxis.MajorStepUnit = Telerik.Charting.TimeInterval.Day;
                    DateTimeAxis.MajorStep = 1;
                    DateTimeAxis.Minimum = DateTime.Now.AddDays(-7);
                    break;

                case "month":
                    DateTimeAxis.LabelFormat = "{0:d/M}";
                    DateTimeAxis.MajorStepUnit = Telerik.Charting.TimeInterval.Week;
                    DateTimeAxis.MajorStep = 1;
                    DateTimeAxis.Minimum = DateTime.Now.AddMonths(-1);
                    break;
                case "year":
                    DateTimeAxis.LabelFormat = "{0:MMM}";
                    DateTimeAxis.MajorStepUnit = Telerik.Charting.TimeInterval.Month;
                    DateTimeAxis.MajorStep = 1;
                    DateTimeAxis.Minimum = DateTime.MinValue;
                    break;

                case "all":
                    DateTimeAxis.LabelFormat = "{0:MMM/yy}";
                    DateTimeAxis.MajorStepUnit = Telerik.Charting.TimeInterval.Month;
                    DateTimeAxis.MajorStep = 4;
                    DateTimeAxis.Minimum = DateTime.MinValue;
                    break;
            }
            return DateTimeAxis;
        }



    }

}

