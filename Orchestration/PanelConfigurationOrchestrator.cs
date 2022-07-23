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
        private static PInvoke.WinEventProc _winEvent;      // keep this as static to prevent garbage collect or the app will crash
        private static IntPtr _winEventHook;
        private uint _prevWinEvent = PInvokeConstant.EVENT_SYSTEM_CAPTUREEND;
        private int _winEventClickLock = 0;
        private Rectangle _lastWindowRectangle;
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
            HookWinEvent();
        }

        public void EndConfiguration()
        {
            UnhookWinEvent();
        }

        public void LockStatusUpdated()
        {
            ActiveProfile.IsLocked = !ActiveProfile.IsLocked;
            ProfileData.WriteProfiles();
        }

        public void PanelConfigPropertyUpdated(int panelIndex, PanelConfigPropertyName configPropertyName)
        {
            if (panelIndex == -1 || !AllowEdit || ActiveProfile.IsLocked)
                return;

            var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(p => p.PanelIndex == panelIndex);

            if (panelConfig != null)
            {
                if (configPropertyName == PanelConfigPropertyName.FullScreen)
                {
                    InputEmulationManager.ToggleFullScreenPanel(panelConfig.PanelHandle);
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
                            WindowActionManager.MoveWindow(panelConfig.PanelHandle, panelConfig.PanelType, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);
                            break;
                        case PanelConfigPropertyName.Width:
                        case PanelConfigPropertyName.Height:
                            if (panelConfig.HideTitlebar)
                                WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, false);

                            WindowActionManager.MoveWindowWithMsfsBugOverrirde(panelConfig.PanelHandle, panelConfig.PanelType, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);

                            if (panelConfig.HideTitlebar)
                                WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, true);

                            break;
                        case PanelConfigPropertyName.AlwaysOnTop:
                            WindowActionManager.ApplyAlwaysOnTop(panelConfig.PanelHandle, panelConfig.PanelType, panelConfig.AlwaysOnTop, new Rectangle(panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height));
                            break;
                        case PanelConfigPropertyName.HideTitlebar:
                            WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, panelConfig.HideTitlebar);
                            break;
                    }
                }

                ProfileData.WriteProfiles();
            }
        }

        public void PanelConfigIncreaseDecrease(int panelIndex, PanelConfigPropertyName configPropertyName, int changeAmount)
        {
            if (panelIndex == -1 || !AllowEdit || ActiveProfile.IsLocked || ActiveProfile.PanelConfigs == null || ActiveProfile.PanelConfigs.Count == 0)
                return;

            var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(p => p.PanelIndex == panelIndex);

            if (panelConfig != null)
            {
                // Should not apply any other settings if panel is full screen mode
                if (panelConfig.FullScreen)
                    return;

                int orignalLeft = panelConfig.Left;

                switch (configPropertyName)
                {
                    case PanelConfigPropertyName.Left:
                        panelConfig.Left += changeAmount;
                        WindowActionManager.MoveWindow(panelConfig.PanelHandle, panelConfig.PanelType, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);
                        break;
                    case PanelConfigPropertyName.Top:
                        panelConfig.Top += changeAmount;
                        WindowActionManager.MoveWindow(panelConfig.PanelHandle, panelConfig.PanelType, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);
                        break;
                    case PanelConfigPropertyName.Width:
                        panelConfig.Width += changeAmount;

                        if (panelConfig.HideTitlebar)
                            WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, false);

                        WindowActionManager.MoveWindowWithMsfsBugOverrirde(panelConfig.PanelHandle, panelConfig.PanelType, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);

                        if (panelConfig.HideTitlebar)
                            WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, true);

                        break;
                    case PanelConfigPropertyName.Height:
                        panelConfig.Height += changeAmount;

                        if (panelConfig.HideTitlebar)
                            WindowActionManager.ApplyHidePanelTitleBar(panelConfig.PanelHandle, false);

                        WindowActionManager.MoveWindowWithMsfsBugOverrirde(panelConfig.PanelHandle, panelConfig.PanelType, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);

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
            // Setup panel config event hooks
            _winEventHook = PInvoke.SetWinEventHook(PInvokeConstant.EVENT_SYSTEM_CAPTURESTART, PInvokeConstant.EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _winEvent, 0, 0, PInvokeConstant.WINEVENT_OUTOFCONTEXT);
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
                    if (hwnd == IntPtr.Zero
                        || idObject != 0
                        || hWinEventHook != _winEventHook
                        || !AllowEdit
                        || ActiveProfile == null
                        || ActiveProfile.PanelConfigs == null
                        || ActiveProfile.PanelConfigs.Count == 0)
                    {
                        return;
                    }

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
                        PInvoke.MoveWindow(panelConfig.PanelHandle, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height, false);
                        break;
                    case PInvokeConstant.EVENT_OBJECT_LOCATIONCHANGE:
                        // Detect if window is maximized, if so, save settings
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

                        //if (!panelConfig.HasTouchableEvent || _prevWinEvent == PInvokeConstant.EVENT_SYSTEM_CAPTUREEND)
                        //    break;

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
                        Rectangle clientRectangle;
                        PInvoke.GetClientRect(panelConfig.PanelHandle, out clientRectangle);

                        panelConfig.Left = winRectangle.Left;
                        panelConfig.Top = winRectangle.Top;

                        if (panelConfig.HideTitlebar)
                        {
                            panelConfig.Width = clientRectangle.Width;
                            panelConfig.Height = clientRectangle.Height;
                        }
                        else
                        {
                            panelConfig.Width = clientRectangle.Width + 16;
                            panelConfig.Height = clientRectangle.Height + 39;
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

                        //if (!panelConfig.HasTouchableEvent || _prevWinEvent == PInvokeConstant.EVENT_SYSTEM_CAPTUREEND)
                        //    break;

                        HandleTouchUpEvent(panelConfig);
                        break;
                }
            }

            _prevWinEvent = iEvent;
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
                    {
                        _isHookMouseDown = true;
                        InputEmulationManager.LeftClickMouseDown(point.X, point.Y);

                        //Debug.WriteLine($"Mouse Down {_pair}");
                    }
                }
            }
        }


        private void HandleTouchUpEvent(PanelConfig panelConfig)
        {
            if (_isHookMouseDown)
            {
                Thread.Sleep(AppSettingData.AppSetting.TouchScreenSettings.MouseUpDownDelay);

                lock (_hookLock)
                {
                    _isHookMouseDown = false;
                    UnhookWinEvent();

                    Point point;
                    PInvoke.GetCursorPos(out point);
                    // Disable left clicking if user is touching the title bar area
                    if (point.Y - panelConfig.Top > (panelConfig.HideTitlebar ? 5 : 31))
                    {
                        InputEmulationManager.LeftClickMouseUp(0, 0);
                        HookWinEvent();
                        // Debug.WriteLine($"Mouse Up {_pair}");
                        //_pair++;

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
            Thread.Sleep(1000);

            if (prevWinEventClickLock == _winEventClickLock)
            {
                if (!_isHookMouseDown)
                {
                    var simulatorProcess = WindowProcessManager.GetSimulatorProcess();
                    var rectangle = WindowActionManager.GetWindowRect(simulatorProcess.Handle);
                    var clientRectangle = WindowActionManager.GetClientRect(simulatorProcess.Handle);

                    PInvoke.SetCursorPos(rectangle.X + clientRectangle.Width / 2, rectangle.Y + clientRectangle.Height / 2);
                }

            }
        }
    }
}
