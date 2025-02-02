﻿using MahApps.Metro.Controls;
using MSFSPopoutPanelManager.UserDataAgent;
using MSFSPopoutPanelManager.WpfApp.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace MSFSPopoutPanelManager.WpfApp
{
    public partial class PreferencesDialog : MetroWindow
    {
        private PreferencesViewModel _preferencesViewModel;

        public PreferencesDialog(PreferencesViewModel preferencesViewModel)
        {
            _preferencesViewModel = preferencesViewModel;

            InitializeComponent();
            this.DataContext = preferencesViewModel;

            _preferencesViewModel.Window = this;
        }

        public AppSetting AppSetting { get; set; }

        public bool ApplicationSettingsVisible { get; set; }

        public bool PopOutSettingsVisible { get; set; }

        public bool AutoPopOutSettingsVisible { get; set; }

        public bool TrackIRSettingsVisible { get; set; }

        public bool TouchSettingsVisible { get; set; }

        public bool MSFSTouchPanelSettingsVisible { get; set; }

        public bool WindowModeSettingsVisible { get; set; }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            var treeViewItem = (TreeViewItem)e.Source;
            _preferencesViewModel.SectionSelectCommand.Execute(treeViewItem.Header.ToString());
        }
    }
}
