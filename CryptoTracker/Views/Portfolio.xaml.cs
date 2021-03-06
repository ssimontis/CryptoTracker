﻿using CryptoTracker.Helpers;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Telerik.UI.Xaml.Controls.Chart;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CryptoTracker {

	public class SuggestionCoinList {
        public string Icon { get; set; }
        public string Name { get; set; }
    }

    public partial class Portfolio : Page {


        internal static ObservableCollection<PurchaseClass> PurchaseList { get; set; }
        internal ObservableCollection<PurchaseClass> NewPurchase { get; set; }
        internal static List<string> coinsArray = App.coinList.Select(x => x.Name).ToList();
        private int EditingPurchaseId { get; set; }

        private bool ShowingDetails = false;
        private double curr = 0;
        private string currTimerange = "month";

        public Portfolio() {
            this.InitializeComponent();

            PurchaseList = ReadPortfolio().Result;
            DataGridd.ItemsSource = PurchaseList;

			PurchaseList.CollectionChanged += PurchaseList_CollectionChanged;
            PurchaseList_CollectionChanged(null, null);

            UpdatePortfolio();
        }

		private void PurchaseList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            PortfolioChartGrid.Visibility = (PurchaseList.Count == 0) ? Visibility.Collapsed : Visibility.Visible;
		}

		private async void Page_Loaded(object sender, RoutedEventArgs e) {
            RadioButton r = new RadioButton { Content = currTimerange };
            TimerangeButton_Click(r, null);
        }


        // ###############################################################################################
        //  For sync all
        internal void UpdatePortfolio() {
            // empty diversification chart
            PortfolioChartGrid.ColumnDefinitions.Clear();
            PortfolioChartGrid.Children.Clear();

            foreach (PurchaseClass purchase in PurchaseList) {
                // this update the ObservableCollection itself
                UpdatePurchase(purchase);

                // create the diversification grid
                ColumnDefinition col = new ColumnDefinition();
                col.Width = new GridLength(purchase.Worth, GridUnitType.Star);
                PortfolioChartGrid.ColumnDefinitions.Add(col);

                var s = new StackPanel();
                s.BorderThickness = new Thickness(0);
                s.Margin = new Thickness(1, 0, 1, 0);
                var t = new TextBlock() {
                    Text = purchase.Crypto,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 7, 0, 7)
                };
                s.Children.Add(t);
                try { s.Background = (SolidColorBrush)App.Current.Resources[purchase.Crypto + "_colorT"]; }
                catch { s.Background = (SolidColorBrush)App.Current.Resources["Main_WhiteBlackT"]; }

                PortfolioChartGrid.Children.Add(s);
                Grid.SetColumn(s, PortfolioChartGrid.Children.Count - 1);
            }

            RadioButton r = new RadioButton { Content = currTimerange };
            TimerangeButton_Click(r, null);
        }

        internal PurchaseClass UpdatePurchase(PurchaseClass purchase) {
            string crypto = purchase.Crypto;

            if (purchase.Current <= 0 || (DateTime.Now - purchase.LastUpdate).TotalSeconds > 20)
                purchase.Current = Math.Round(App.GetCurrentPrice(crypto, "defaultMarket"), 4);

            curr = purchase.Current;
            purchase.Worth = Math.Round(curr * purchase.CryptoQty, 2);

            // If the user has also filled the invested quantity, we can calculate everything else
            if (purchase.InvestedQty > 0) {
                double priceBought = (1 / purchase.CryptoQty) * purchase.InvestedQty;
                priceBought = Math.Round(priceBought, 4);

                double earningz = Math.Round((curr - priceBought) * purchase.CryptoQty, 4);
                purchase.arrow = earningz < 0 ? "▼" : "▲";
                purchase.BoughtAt = priceBought;
                purchase.Delta = Math.Round(curr / priceBought, 2) * 100;
                if (purchase.Delta > 100)
                    purchase.Delta -= 100;
                purchase.Profit = Math.Round(Math.Abs(earningz), 2);
                purchase.ProfitFG = (earningz < 0) ? (SolidColorBrush)App.Current.Resources["pastelRed"] : (SolidColorBrush)App.Current.Resources["pastelGreen"];
            }
            
            return purchase;
        }

        // ###############################################################################################
        private void GoToCoinPortfolio_Click(object sender, RoutedEventArgs e) {
            var menu = sender as MenuFlyoutItem;
            var item = menu.DataContext as PurchaseClass;
            this.Frame.Navigate(typeof(CoinDetails), item.Crypto);
        }

        private void RemovePortfolio_Click(object sender, RoutedEventArgs e) {
            var menu = sender as MenuFlyoutItem;
            var item = menu.DataContext as PurchaseClass;
            var items = DataGridd.ItemsSource.Cast<PurchaseClass>().ToList();
            var index = items.IndexOf(item);
            PurchaseList.RemoveAt(index);
            UpdatePortfolio();
            SavePortfolio();
        }        


        // ###############################################################################################
        //  Read/Write portfolio
        private static async void SavePortfolio() {
            try {
                StorageFile savedStuffFile =
                    await ApplicationData.Current.LocalFolder.CreateFileAsync("portfolio", CreationCollisionOption.ReplaceExisting);

                using (Stream writeStream =
                    await savedStuffFile.OpenStreamForWriteAsync().ConfigureAwait(false) ) {

                    DataContractSerializer stuffSerializer =
                        new DataContractSerializer(typeof(ObservableCollection<PurchaseClass>));

                    stuffSerializer.WriteObject(writeStream, PurchaseList);
                    await writeStream.FlushAsync();
                    writeStream.Dispose();

                }
            } catch (Exception e) {
                var z = e.Message;
            }
        }
        private static async Task<ObservableCollection<PurchaseClass>> ReadPortfolio() {

            try {
                var readStream =
                    await ApplicationData.Current.LocalFolder.OpenStreamForReadAsync("portfolio").ConfigureAwait(false);

                DataContractSerializer stuffSerializer =
                    new DataContractSerializer(typeof(ObservableCollection<PurchaseClass>));

                var setResult = (ObservableCollection<PurchaseClass>)stuffSerializer.ReadObject(readStream);
                await readStream.FlushAsync();
                readStream.Dispose();

                return setResult;
            } catch (Exception ex) {
                var unusedWarning = ex.Message;
                return new ObservableCollection<PurchaseClass>();
            }
        }

        internal static void importPortfolio(ObservableCollection<PurchaseClass>portfolio) {
            PurchaseList = new ObservableCollection<PurchaseClass>(portfolio);
            SavePortfolio();
        }

        private void ToggleDetails_click(object sender, RoutedEventArgs e) {
            ShowingDetails = !ShowingDetails;
            if (ShowingDetails) {
                DataGridd.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible;
                DataGridd.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
                PortfolioChart.Visibility = Visibility.Collapsed;
                TimerangeRadioButtons.Visibility = Visibility.Collapsed;
                MainGrid.RowDefinitions[2].Height = new GridLength(0, GridUnitType.Star);
                MainGrid.RowDefinitions[3].Height = new GridLength(0, GridUnitType.Star);
            }
            else {
                DataGridd.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;
                DataGridd.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
                PortfolioChart.Visibility = Visibility.Visible;
                TimerangeRadioButtons.Visibility = Visibility.Visible;
                MainGrid.RowDefinitions[2].Height = new GridLength(2, GridUnitType.Star);
                MainGrid.RowDefinitions[3].Height = new GridLength(1, GridUnitType.Auto);
            }
        }

        // ###############################################################################################
        // Add/Edit purchase dialog
        private void AddPurchase_click(object sender, RoutedEventArgs e) {
            NewPurchase = new ObservableCollection<PurchaseClass>() { new PurchaseClass() };
            TestRepeater.ItemsSource = NewPurchase;
            PurchaseDialog.Title = "💵 New purchase";
            PurchaseDialog.PrimaryButtonText = "Add";
            PurchaseDialog.ShowAsync();
        }

        private void EditPurchase_Click(object sender, RoutedEventArgs e) {
            var purchase = ((PurchaseClass)((FrameworkElement)sender).DataContext);
            EditingPurchaseId = PurchaseList.IndexOf(purchase);
            NewPurchase = new ObservableCollection<PurchaseClass>() { purchase };
            TestRepeater.ItemsSource = NewPurchase;
            PurchaseDialog.Title = "💵 Edit purchase";
            PurchaseDialog.PrimaryButtonText = "Save";
            PurchaseDialog.ShowAsync();
        }

        private void PurchaseDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) {
            if (string.IsNullOrEmpty(NewPurchase[0].Crypto) || NewPurchase[0].CryptoQty <= 0 || NewPurchase[0].InvestedQty <= 0) {
                args.Cancel = true;
                new MessageDialog("Error.").ShowAsync();
            }
            else {
                if (sender.PrimaryButtonText == "Add") {
                    // Get logo for the coin
                    var crypto = NewPurchase[0].Crypto;
                    string logoURL = "Assets/Icons/icon" + crypto + ".png";
                    if (!File.Exists(logoURL))
                        NewPurchase[0].CryptoLogo = "https://chasing-coins.com/coin/logo/" + crypto;
                    else
                        NewPurchase[0].CryptoLogo = "/" + logoURL;
                    PurchaseList.Add(NewPurchase[0]);
                }
                else if(sender.PrimaryButtonText == "Save") {
                    PurchaseList.RemoveAt(EditingPurchaseId);
                    PurchaseList.Insert(EditingPurchaseId, NewPurchase[0]);
                }
                // Update and save
                UpdatePortfolio();
                SavePortfolio();
            }
        }

        private void DialogBtn_LostFocus(object sender, RoutedEventArgs e) {
            // If we change the crypto, set the current price to 0 so everything updates
            if (sender.GetType().Name == "ComboBox")
                NewPurchase[0].Current = 0;

            // If we have the coin and the quantity, we can update some properties
            if (!string.IsNullOrEmpty(NewPurchase[0].Crypto) && NewPurchase[0].CryptoQty > 0)
                NewPurchase[0] = UpdatePurchase(NewPurchase[0]);
        }

        private void DataGrid_Sorting(object sender, DataGridColumnEventArgs e) {
            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
                e.Column.SortDirection = DataGridSortDirection.Ascending;
            else
                e.Column.SortDirection = DataGridSortDirection.Descending;

            switch (e.Column.Header) {
                case "Crypto":
                    if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
                        DataGridd.ItemsSource = new ObservableCollection<PurchaseClass>(from item in PurchaseList
                                                                                        orderby item.Crypto ascending
                                                                                        select item);
                    else
                        DataGridd.ItemsSource = new ObservableCollection<PurchaseClass>(from item in PurchaseList
                                                                                        orderby item.Crypto descending
                                                                                        select item);
                    break;
                case "Invested":
                    if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
                        DataGridd.ItemsSource = new ObservableCollection<PurchaseClass>(from item in PurchaseList
                                                                                        orderby item.InvestedQty ascending
                                                                                        select item);
                    else
                        DataGridd.ItemsSource = new ObservableCollection<PurchaseClass>(from item in PurchaseList
                                                                                        orderby item.InvestedQty descending
                                                                                        select item);
                    break;
                case "Worth":
                    if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
                        DataGridd.ItemsSource = new ObservableCollection<PurchaseClass>(from item in PurchaseList
                                                                                        orderby item.Worth ascending
                                                                                        select item);
                    else
                        DataGridd.ItemsSource = new ObservableCollection<PurchaseClass>(from item in PurchaseList
                                                                                        orderby item.Worth descending
                                                                                        select item);
                    break;
                case "Currently":
                    if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
                        DataGridd.ItemsSource = new ObservableCollection<PurchaseClass>(from item in PurchaseList
                                                                                        orderby item.Current ascending
                                                                                        select item);
                    else 
                        DataGridd.ItemsSource = new ObservableCollection<PurchaseClass>(from item in PurchaseList
                                                                                        orderby item.Current descending
                                                                                        select item);
                    break;
            }
            foreach (var dgColumn in DataGridd.Columns) {
                if (dgColumn.Header.ToString() != e.Column.Header.ToString())
                    dgColumn.SortDirection = null;
            }
        }

        private async void TimerangeButton_Click(object sender, RoutedEventArgs e) {
            var nPurchases = PurchaseList.Count;
            if (nPurchases == 0)
                return;

            RadioButton btn = sender as RadioButton;
            currTimerange = btn.Content.ToString();

            var t = App.ParseTimeSpan(currTimerange);
            int limit = t.Item2;


            var k = new List<List<JSONhistoric>>(nPurchases);
            foreach (PurchaseClass purchase in PurchaseList) {
                var hist = await App.GetHistoricalPrices(purchase.Crypto, currTimerange);
                k.Add(hist);
            }

            var dates_arr = k[0].Select(kk => kk.DateTime).ToList();
            var arr = k.Select(kk => kk.Select(kkk => kkk.High)).ToArray();
            var prices = new List<List<double>>();
            for (int i = 0; i < arr.Length; i++) {
                prices.Add(arr[i].Select(a => a * PurchaseList[i].CryptoQty).ToList());
            }

            var prices_arr = new List<double>();
            var min_limit = prices.Select(x => x.Count).Min();
            for (int i = 0; i < min_limit; i++) {
                prices_arr.Add(prices.Select(p => p[i]).Sum());
            }

            List<ChartData> data = new List<ChartData>();
            for (int i = 0; i < min_limit; ++i) {
                data.Add(new ChartData {
                    Date = dates_arr[i],
                    Value = (float)prices_arr[i]
                });
            }
            var series = (SplineAreaSeries)PortfolioChart.Series[0];
            series.CategoryBinding = new PropertyNameDataPointBinding() { PropertyName = "Date" };
            series.ValueBinding = new PropertyNameDataPointBinding() { PropertyName = "Value" };
            series.ItemsSource = data;
            verticalAxis.Minimum = GraphHelper.GetMinimumOfArray(data.Select(d => d.Value).ToList());
            verticalAxis.Maximum = GraphHelper.GetMaximumOfArray(data.Select(d => d.Value).ToList());
            dateTimeAxis = App.AdjustAxis(dateTimeAxis, currTimerange);
        }
    }
}
