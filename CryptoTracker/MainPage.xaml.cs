﻿using CryptoTracker.Helpers;
using CryptoTracker.Views;
using Microsoft.AppCenter.Analytics;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace CryptoTracker {
    public sealed partial class MainPage : Page {

        public MainPage() {
            this.InitializeComponent();

            // Clear the current tile
            //TileUpdateManager.CreateTileUpdaterForApplication().Clear();

            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;

            if (Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Desktop") {
                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            }

            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar")) {
                var statusBar = StatusBar.GetForCurrentView();

                if (App.localSettings.Values["Theme"].Equals("Dark")) {
                    statusBar.BackgroundColor = Color.FromArgb(255, 23, 23, 23); //31 31 31
                    statusBar.BackgroundOpacity = 1;
                    statusBar.ForegroundColor = Color.FromArgb(255, 255, 255, 255);
                } else {
                    statusBar.BackgroundColor = Color.FromArgb(255, 242, 242, 242); // 230
                    statusBar.BackgroundOpacity = 1;
                    statusBar.ForegroundColor = Color.FromArgb(255, 0, 0, 0);
                }

                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
            }

            FirstRunDialogHelper.ShowIfAppropriateAsync();
            //ContentFrame.Navigate(typeof(Home));

            // Extend acrylic
            ExtendAcrylicIntoTitleBar();
        }

        private void App_BackRequested(object sender, BackRequestedEventArgs e) {
            if (ContentFrame == null)
                return;

            // Navigate back if possible, and if the event has not already been handled .
            if (ContentFrame.CanGoBack && e.Handled == false) {
                e.Handled = true;
                ContentFrame.GoBack();
            }
        }

        /// Extend acrylic into the title bar. 
        private void ExtendAcrylicIntoTitleBar() {
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.BackgroundColor = Colors.Transparent;
            titleBar.InactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Color.FromArgb(255, 150, 150, 150);
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 150, 150, 150);

            Window.Current.SetTitleBar(AppTitle);
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        internal void UpdateButton_Click(object sender, RoutedEventArgs e) {

            switch (ContentFrame.SourcePageType.Name) {
                case "CoinDetails":
                    var p0 = (CoinDetails)ContentFrame.Content;
                    p0.UpdatePage();
                    break;
                case "Home":
                    var p2 = (Home)ContentFrame.Content;
                    p2.UpdateAllCards();
                    break;
                case "Portfolio":
                    var p1 = (Portfolio)ContentFrame.Content;
                    p1.UpdatePortfolio();
                    break;
            }

            LiveTile l = new LiveTile();
            l.UpdateLiveTile();
        }


        private void NavView_Loaded(object sender, RoutedEventArgs e) {
            // set the initial SelectedItem 
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void NavView_Sync_Tapped(object sender, TappedRoutedEventArgs e) {
            UpdateButton_Click(sender, e);
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            if (args.IsSettingsSelected) {
                ContentFrame.Navigate(typeof(Settings));
            } else {
                switch (((FrameworkElement)args.SelectedItem).Tag) {
                    case "home":
                        ContentFrame.Navigate(typeof(Home));
                        break;
                    case "top":
                        ContentFrame.Navigate(typeof(Top100));
                        break;
                    case "news":
                        ContentFrame.Navigate(typeof(News));
                        break;
                    case "portfolio":
                        ContentFrame.Navigate(typeof(Portfolio));
                        break;
                }
            }
        }
    }
}
