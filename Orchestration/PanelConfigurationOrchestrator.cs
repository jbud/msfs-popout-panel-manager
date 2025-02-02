﻿using MSFSPopoutPanelManager.Shared;
using MSFSPopoutPanelManager.UserDataAgent;
using MSFSPopoutPanelManager.WindowsAgent;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MSFSPopoutPanelManager.Orchestration
{
    public class PanelConfigurationOrchestrator : ObservableObject
    {
        private static WindowProcess _simulatorProcess;
        private static PInvoke.WinEventProc _winEvent;      // keep this as static to prevent garbage collect or the app will crash
        private static IntPtr _winEventHook;
        private Rectangle _lastWindowRectangle;
        private IntPtr _panelHandleDisableRefresh = IntPtr.Zero;

        private uint _prevWinEvent = PInvokeConstant.EVENT_SYSTEM_CAPTUREEND;
        private int _winEventClickLock = 0;
        private object _hookLock = new object();
        private bool _isHookMouseDown = false;

        public PanelConfigurationOrchestrator()
        {
            _winEvent = new PInvoke.WinEventProc(EventCallback);
            AllowEdit = true;
        }

        internal ProfileData ProfileData { get; set; }

        internal AppSettingData AppSettingData { get; set; }

        private Profile ActiveProfile { get { return ProfileData == null ? null : ProfileData.ActiveProfile; } }

        public bool AllowEdit { get; set; }

        public void StartConfiguration()
        {
            _simulatorProcess = WindowProcessManager.GetSimulatorProcess();

            HookWinEvent();

            TouchEventManager.ActiveProfile = ProfileData.ActiveProfile;
            TouchEventManager.AppSetting = AppSettingData.AppSetting;

            if (ActiveProfile.PanelConfigs.Any(p => p.TouchEnabled) && !TouchEventManager.IsHooked)
            {
                TouchEventManager.Hook();
            }
        }

        public void EndConfiguration()
        {
            UnhookWinEvent();
            TouchEventManager.UnHook();
        }

        public void LockStatusUpdated()
        {
            ActiveProfile.IsLocked = !ActiveProfile.IsLocked;
            ProfileData.WriteProfiles();
        }

        public void PanelConfigPropertyUpdated(IntPtr panelHandle, PanelConfigPropertyName configPropertyName)
        {
            if (panelHandle == IntPtr.Zero || !AllowEdit || ActiveProfile.IsLocked)
                return;

            var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(p => p.PanelHandle == panelHandle);

            if (panelConfig != null)
            {
                if (configPropertyName == PanelConfigPropertyName.FullScreen)
                {
                    InputEmulationManager.ToggleFullScreenPanel(panelConfig.PanelHandle);

                    // Set full screen mode panel coordinate
                    var windowRectangle = WindowActionManager.GetWindowRect(panelConfig.PanelHandle);
                    var clientRectangle = WindowActionManager.GetClientRect(panelConfig.PanelHandle);
                    panelConfig.FullScreenLeft = panelConfig.FullScreen ? windowRectangle.Left : 0;
                    panelConfig.FullScreenTop = panelConfig.FullScreen ? windowRectangle.Top : 0;
                    panelConfig.FullScreenWidth = panelConfig.FullScreen ? clientRectangle.Width : 0;
                    panelConfig.FullScreenHeight = panelConfig.FullScreen ? clientRectangle.Height : 0;

                    panelConfig.HideTitlebar = false;
                    panelConfig.AlwaysOnTop = false;
                }
                else if (configPropertyName == PanelConfigPropertyName.PanelName)
                {
                    var name = panelConfig.PanelName;

                    if (panelConfig.PanelType == PanelType.CustomPopout && name.IndexOf("(Custom)") == -1)
                    {
                        name = name + " (Custom)";
                        PInvoke.SetWindowText(panelConfig.PanelHandle, name);
                    }
                }
                else if (!panelConfig.FullScreen)
                {
                    switch (configPropertyName)
                    {
                        case PanelConfigPropertyName.Left:
                        case PanelConfigPropertyName.Top:
                            _panelHandleDisableRefresh = panelConfig.PanelHandle;
                            WindowActionManager.MoveWindow(panelConfig.PanelHandle, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);
                            break;
                        case PanelConfigPropertyName.Width:
                        case PanelConfigPropertyName.Height:
                            _panelHandleDisableRefresh = panelConfig.PanelHandle;

                            if (panelConfig.HideTitlebar)
                                WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, false);

                            WindowActionManager.MoveWindowWithMsfsBugOverrirde(panelConfig.PanelHandle, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);

                            if (panelConfig.HideTitlebar)
                                WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, true);

                            break;
                        case PanelConfigPropertyName.AlwaysOnTop:
                            WindowActionManager.ApplyAlwaysOnTop(panelConfig.PanelHandle, panelConfig.PanelType, panelConfig.AlwaysOnTop, new Rectangle(panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height));
                            break;
                        case PanelConfigPropertyName.HideTitlebar:
                            _panelHandleDisableRefresh = panelConfig.PanelHandle;
                            WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, panelConfig.HideTitlebar);
                            break;
                        case PanelConfigPropertyName.TouchEnabled:
                            if (ActiveProfile.PanelConfigs.Any(p => p.TouchEnabled) && !TouchEventManager.IsHooked)
                                TouchEventManager.Hook();
                            else if (ActiveProfile.PanelConfigs.All(p => !p.TouchEnabled) && TouchEventManager.IsHooked)
                                TouchEventManager.UnHook();

                            if (!panelConfig.TouchEnabled)
                                panelConfig.DisableGameRefocus = false;
                            break;
                    }
                }

                ProfileData.WriteProfiles();
            }
        }

        public void PanelConfigIncreaseDecrease(IntPtr panelHandle, PanelConfigPropertyName configPropertyName, int changeAmount)
        {
            if (panelHandle == IntPtr.Zero || !AllowEdit || ActiveProfile.IsLocked || ActiveProfile.PanelConfigs == null || ActiveProfile.PanelConfigs.Count == 0)
                return;

            var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(p => p.PanelHandle == panelHandle);

            if (panelConfig != null)
            {
                // Should not apply any other settings if panel is full screen mode
                if (panelConfig.FullScreen)
                    return;

                int orignalLeft = panelConfig.Left;

                switch (configPropertyName)
                {
                    case PanelConfigPropertyName.Left:
                        _panelHandleDisableRefresh = panelConfig.PanelHandle;
                        panelConfig.Left += changeAmount;
                        WindowActionManager.MoveWindow(panelConfig.PanelHandle, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);
                        break;
                    case PanelConfigPropertyName.Top:
                        _panelHandleDisableRefresh = panelConfig.PanelHandle;
                        panelConfig.Top += changeAmount;
                        WindowActionManager.MoveWindow(panelConfig.PanelHandle, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);
                        break;
                    case PanelConfigPropertyName.Width:
                        _panelHandleDisableRefresh = panelConfig.PanelHandle;
                        panelConfig.Width += changeAmount;

                        if (panelConfig.HideTitlebar)
                            WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, false);

                        WindowActionManager.MoveWindowWithMsfsBugOverrirde(panelConfig.PanelHandle, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);

                        if (panelConfig.HideTitlebar)
                            WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, true);

                        break;
                    case PanelConfigPropertyName.Height:
                        _panelHandleDisableRefresh = panelConfig.PanelHandle;
                        panelConfig.Height += changeAmount;

                        if (panelConfig.HideTitlebar)
                            WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, false);

                        WindowActionManager.MoveWindowWithMsfsBugOverrirde(panelConfig.PanelHandle, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);

                        if (panelConfig.HideTitlebar)
                            WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, true);

                        break;
                    default:
                        return;
                }

                ProfileData.WriteProfiles();
            }
        }

        private void HookWinEvent()
        {
            if (ActiveProfile == null || ActiveProfile.PanelConfigs == null || ActiveProfile.PanelConfigs.Count == 0)
                return;

            // Setup panel config event hooks
            if (ActiveProfile.RealSimGearGTN750Gen1Override && AppSettingData.AppSetting.TouchScreenSettings.RealSimGearGTN750Gen1Override)
                _winEventHook = PInvoke.SetWinEventHook(PInvokeConstant.EVENT_SYSTEM_CAPTURESTART, PInvokeConstant.EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _winEvent, 0, 0, PInvokeConstant.WINEVENT_OUTOFCONTEXT);
            else
                _winEventHook = PInvoke.SetWinEventHook(PInvokeConstant.EVENT_SYSTEM_MOVESIZEEND, PInvokeConstant.EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _winEvent, 0, 0, PInvokeConstant.WINEVENT_OUTOFCONTEXT);
        }

        private void UnhookWinEvent()
        {
            // Unhook all Win API events
            PInvoke.UnhookWinEvent(_winEventHook);
        }

        private void EventCallback(IntPtr hWinEventHook, uint iEvent, IntPtr hwnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime)
        {
            switch (iEvent)
            {
                case PInvokeConstant.EVENT_OBJECT_LOCATIONCHANGE:
                case PInvokeConstant.EVENT_SYSTEM_MOVESIZEEND:
                case PInvokeConstant.EVENT_SYSTEM_CAPTURESTART:
                case PInvokeConstant.EVENT_SYSTEM_CAPTUREEND:
                    // check by priority to speed up comparing of escaping constraints
                    if (hwnd == IntPtr.Zero || idObject != 0 || hWinEventHook != _winEventHook || !AllowEdit)
                        return;

                    HandleEventCallback(hwnd, iEvent);
                    break;
                default:
                    break;
            }
        }

        private void HandleEventCallback(IntPtr hwnd, uint iEvent)
        {
            var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(panel => panel.PanelHandle == hwnd);

            if (panelConfig == null)
                return;

            // Should not apply any other settings if panel is full screen mode
            if (panelConfig.FullScreen)
                return;

            if (panelConfig.IsLockable && ActiveProfile.IsLocked)
            {
                switch (iEvent)
                {
                    case PInvokeConstant.EVENT_SYSTEM_MOVESIZEEND:
                        // Move window back to original location
                        WindowActionManager.MoveWindow(panelConfig.PanelHandle, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);
                        break;
                    case PInvokeConstant.EVENT_OBJECT_LOCATIONCHANGE:
                        WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
                        wp.length = System.Runtime.InteropServices.Marshal.SizeOf(wp);
                        PInvoke.GetWindowPlacement(hwnd, ref wp);
                        if (wp.showCmd == PInvokeConstant.SW_SHOWMAXIMIZED || wp.showCmd == PInvokeConstant.SW_SHOWMINIMIZED || wp.showCmd == PInvokeConstant.SW_SHOWNORMAL)
                        {
                            PInvoke.ShowWindow(hwnd, PInvokeConstant.SW_RESTORE);
                        }
                        break;
                    case PInvokeConstant.EVENT_SYSTEM_CAPTURESTART:
                        if (!panelConfig.HasTouchableEvent || _prevWinEvent == PInvokeConstant.EVENT_SYSTEM_CAPTURESTART)
                            break;

                        HandleTouchDownEvent(panelConfig);
                        break;
                    case PInvokeConstant.EVENT_SYSTEM_CAPTUREEND:
                        if (!panelConfig.TouchEnabled || _prevWinEvent == PInvokeConstant.EVENT_OBJECT_LOCATIONCHANGE)
                            break;
                        HandleTouchUpEvent(panelConfig);
                        break;
                }
            }
            else
            {
                switch (iEvent)
                {
                    case PInvokeConstant.EVENT_OBJECT_LOCATIONCHANGE:
                        Rectangle winRectangle;
                        PInvoke.GetWindowRect(panelConfig.PanelHandle, out winRectangle);

                        if (_lastWindowRectangle == winRectangle)       // ignore duplicate callback messages
                            return;

                        _lastWindowRectangle = winRectangle;

                        if (_panelHandleDisableRefresh != IntPtr.Zero)
                        {
                            _panelHandleDisableRefresh = IntPtr.Zero;
                            return;
                        }

                        panelConfig.Left = winRectangle.Left;
                        panelConfig.Top = winRectangle.Top;

                        if (!panelConfig.HideTitlebar)
                        {
                            panelConfig.Width = winRectangle.Width - winRectangle.Left;
                            panelConfig.Height = winRectangle.Height - winRectangle.Top;
                        }
                        else
                        {
                            panelConfig.Width = winRectangle.Width - winRectangle.Left - 16;
                            panelConfig.Height = winRectangle.Height - winRectangle.Top - 39;
                        }

                        // Detect if window is maximized, if so, save settings
                        WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
                        wp.length = System.Runtime.InteropServices.Marshal.SizeOf(wp);
                        PInvoke.GetWindowPlacement(hwnd, ref wp);
                        if (wp.showCmd == PInvokeConstant.SW_SHOWMAXIMIZED || wp.showCmd == PInvokeConstant.SW_SHOWMINIMIZED)
                        {
                            ProfileData.WriteProfiles();
                        }

                        break;
                    case PInvokeConstant.EVENT_SYSTEM_MOVESIZEEND:
                        ProfileData.WriteProfiles();
                        break;
                    case PInvokeConstant.EVENT_SYSTEM_CAPTURESTART:
                        if (!panelConfig.HasTouchableEvent || _prevWinEvent == PInvokeConstant.EVENT_SYSTEM_CAPTURESTART)
                            break;

                        HandleTouchDownEvent(panelConfig);
                        break;
                    case PInvokeConstant.EVENT_SYSTEM_CAPTUREEND:
                        if (!panelConfig.TouchEnabled || _prevWinEvent == PInvokeConstant.EVENT_OBJECT_LOCATIONCHANGE)
                            break;
                        HandleTouchUpEvent(panelConfig);
                        break;
                }
            }
        }

        private void HandleTouchDownEvent(PanelConfig panelConfig)
        {
            if (!_isHookMouseDown)
            {
                lock (_hookLock)
                {
                    Point point;
                    PInvoke.GetCursorPos(out point);

                    // Disable left clicking if user is touching the title bar area
                    if (point.Y - panelConfig.Top > (panelConfig.HideTitlebar ? 5 : 31))
                        _isHookMouseDown = true;
                }
            }
        }


        private void HandleTouchUpEvent(PanelConfig panelConfig)
        {
            if (_isHookMouseDown)
            {
                Thread.Sleep(AppSettingData.AppSetting.TouchScreenSettings.TouchDownUpDelay);

                lock (_hookLock)
                {
                    _isHookMouseDown = false;

                    Point point;
                    PInvoke.GetCursorPos(out point);

                    // Disable left clicking if user is touching the title bar area
                    if (point.Y - panelConfig.Top > (panelConfig.HideTitlebar ? 5 : 31))
                    {
                        var prevWinEventClickLock = ++_winEventClickLock;

                        if (prevWinEventClickLock == _winEventClickLock && AppSettingData.AppSetting.TouchScreenSettings.RefocusGameWindow)
                        {
                            Task.Run(() => RefocusMsfs(prevWinEventClickLock));
                        }
                    }
                }
            }
        }

        private void RefocusMsfs(int prevWinEventClickLock)
        {
            Thread.Sleep(AppSettingData.AppSetting.TouchScreenSettings.RefocusGameWindowDelay);

            if (prevWinEventClickLock == _winEventClickLock)
            {
                if (!_isHookMouseDown)
                {
                    var rectangle = WindowActionManager.GetWindowRect(_simulatorProcess.Handle);
                    var clientRectangle = WindowActionManager.GetClientRect(_simulatorProcess.Handle);
                    PInvoke.SetCursorPos(rectangle.X + clientRectangle.Width / 2, rectangle.Y + clientRectangle.Height / 2);
                }
            }
        }
    }
}
