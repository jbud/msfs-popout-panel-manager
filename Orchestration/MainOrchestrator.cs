﻿using AutoUpdaterDotNET;
using MSFSPopoutPanelManager.Shared;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace MSFSPopoutPanelManager.Orchestration
{
    public class MainOrchestrator : ObservableObject
    {
        private const int MSFS_GAME_EXIT_DETECTION_INTERVAL = 3000;
        private System.Timers.Timer _msfsGameExitDetectionTimer;

        public MainOrchestrator()
        {
            Profile = new ProfileOrchestrator();
            PanelSource = new PanelSourceOrchestrator();
            PanelPopOut = new PanelPopOutOrchestrator();
            PanelConfiguration = new PanelConfigurationOrchestrator();
            FlightSim = new FlightSimOrchestrator();
            OnlineFeature = new OnlineFeatureOrchestrator();
            TouchPanel = new TouchPanelOrchestrator();

            FlightSimData = new FlightSimData();
            FlightSimData.CurrentMsfsAircraftChanged += (sernder, e) => { ProfileData.RefreshProfile(); ProfileData.AutoSwitchProfile(); };
            FlightSimData.CurrentMsfsLiveryTitleChanged += (sernder, e) => { ProfileData.MigrateLiveryToAircraftBinding(); ProfileData.AutoSwitchProfile(); };

            AppSettingData = new AppSettingData();
            AppSettingData.TouchPanelIntegrationChanged += async (sender, e) =>
            {
                await TouchPanel.TouchPanelIntegrationUpdated(e);
            };

            ProfileData = new ProfileData();
            ProfileData.FlightSimData = FlightSimData;
            ProfileData.AppSettingData = AppSettingData;
            ProfileData.ActiveProfileChanged += (sender, e) =>
            {
                PanelSource.CloseAllPanelSource();
            };
        }

        public ProfileOrchestrator Profile { get; set; }

        public PanelSourceOrchestrator PanelSource { get; set; }

        public PanelPopOutOrchestrator PanelPopOut { get; set; }

        public PanelConfigurationOrchestrator PanelConfiguration { get; set; }

        public TouchPanelOrchestrator TouchPanel { get; set; }

        public ProfileData ProfileData { get; set; }

        public AppSettingData AppSettingData { get; private set; }

        public FlightSimData FlightSimData { get; private set; }

        public FlightSimOrchestrator FlightSim { get; set; }

        public OnlineFeatureOrchestrator OnlineFeature { get; set; }

        public IntPtr ApplicationHandle { get; set; }

        public async void Initialize()
        {
            MigrateData.MigrateUserDataFiles();

            AppSettingData.ReadSettings();
            ProfileData.ReadProfiles();

            Profile.ProfileData = ProfileData;
            Profile.FlightSimData = FlightSimData;

            PanelSource.ProfileData = ProfileData;
            PanelSource.AppSettingData = AppSettingData;
            PanelSource.FlightSimData = FlightSimData;
            PanelSource.FlightSimOrchestrator = FlightSim;

            PanelPopOut.ProfileData = ProfileData;
            PanelPopOut.AppSettingData = AppSettingData;
            PanelPopOut.FlightSimData = FlightSimData;
            PanelPopOut.FlightSimOrchestrator = FlightSim;
            PanelPopOut.PanelSourceOrchestrator = PanelSource;
            PanelPopOut.TouchPanelOrchestrator = TouchPanel;
            PanelPopOut.OnPopOutCompleted += (sender, e) => TouchPanel.ReloadTouchPanelSimConnectDataDefinition();

            PanelConfiguration.ProfileData = ProfileData;
            PanelConfiguration.AppSettingData = AppSettingData;

            FlightSim.ProfileData = ProfileData;
            FlightSim.AppSettingData = AppSettingData;
            FlightSim.FlightSimData = FlightSimData;
            FlightSim.OnFlightStartedForAutoPopOut += (sender, e) => PanelPopOut.AutoPopOut();
            FlightSim.OnSimulatorStarted += (sender, e) => DetectMsfsExit();

            TouchPanel.ProfileData = ProfileData;
            TouchPanel.AppSettingData = AppSettingData;
            TouchPanel.ApplicationHandle = ApplicationHandle;

            CheckForAutoUpdate();

            ProfileData.UpdateActiveProfile(AppSettingData.AppSetting.LastUsedProfileId);     // Load last used profile
            FlightSim.StartSimConnectServer();                                                // Start the SimConnect server

            // Enable/Disable touch panel feature (Personal use only feature)
            try
            {
                var assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "WebServer.dll"));
                if (assembly != null)
                    AppSettingData.AppSetting.IsEnabledTouchPanelServer = true;

                if (AppSettingData.AppSetting.TouchPanelSettings.EnableTouchPanelIntegration)
                    await TouchPanel.StartServer();

                assembly = null;
            }
            catch { }
        }

        public async Task ApplicationClose()
        {
            // Force unhook all win events 
            PanelConfiguration.EndConfiguration();
            FlightSim.EndSimConnectServer(true);
            await TouchPanel.StopServer();
        }

        private void CheckForAutoUpdate()
        {
            string jsonPath = Path.Combine(Path.Combine(FileIo.GetUserDataFilePath(), "autoupdate.json"));
            AutoUpdater.PersistenceProvider = new JsonFilePersistenceProvider(jsonPath);
            AutoUpdater.Synchronous = true;
            AutoUpdater.AppTitle = "MSFS Pop Out Panel Manager";
            AutoUpdater.RunUpdateAsAdmin = false;
            AutoUpdater.UpdateFormSize = new System.Drawing.Size(930, 675);
            AutoUpdater.Start(AppSettingData.AppSetting.AutoUpdaterUrl);
        }

        private void DetectMsfsExit()
        {
            _msfsGameExitDetectionTimer = new System.Timers.Timer();
            _msfsGameExitDetectionTimer.Interval = MSFS_GAME_EXIT_DETECTION_INTERVAL;
            _msfsGameExitDetectionTimer.Enabled = true;
            _msfsGameExitDetectionTimer.Elapsed += async (source, e) =>
            {
                if (WindowsAgent.WindowProcessManager.GetSimulatorProcess() == null)
                {
                    await ApplicationClose();
                    Environment.Exit(0);
                }
            };
        }
    }
}
