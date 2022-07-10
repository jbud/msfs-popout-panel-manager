﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace MSFSPopoutPanelManager.Provider
{
    public class InputEmulationManager
    {
        const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        const uint MOUSEEVENTF_LEFTUP = 0x04;
        const uint KEYEVENTF_EXTENDEDKEY = 0x01;
        const uint KEYEVENTF_KEYDOWN = 0x0;
        const uint KEYEVENTF_KEYUP = 0x2;
        const uint VK_RMENU = 0xA5;
        const uint VK_LMENU = 0xA4;
        const uint VK_LCONTROL = 0xA2;
        const uint VK_SPACE = 0x20;
        const uint VK_ENT = 0x0D;
        const uint KEY_0 = 0x30;

        public static void LeftClick(int x, int y)
        {
            PInvoke.SetCursorPos(x, y);
            PInvoke.SetCursorPos(x, y);
            Thread.Sleep(300);

            PInvoke.mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
            Thread.Sleep(200);
            PInvoke.mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
        }

        public static void LeftClickFast(int x, int y)
        {
            PInvoke.mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
            Thread.Sleep(50);
            PInvoke.mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
        }

        public static void PopOutPanel(int x, int y)
        {
            LeftClick(x, y);
            Thread.Sleep(300);

            PInvoke.SetCursorPos(x, y);
            PInvoke.SetCursorPos(x, y);
            Thread.Sleep(300);

            PInvoke.keybd_event(Convert.ToByte(VK_RMENU), 0, KEYEVENTF_EXTENDEDKEY, 0);
            PInvoke.mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
            Thread.Sleep(200);
            PInvoke.mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
            PInvoke.keybd_event(Convert.ToByte(VK_RMENU), 0, KEYEVENTF_KEYUP, 0);
        }

        public static void CenterView(IntPtr hwnd)
        {
            Rectangle rectangle;
            PInvoke.GetWindowRect(hwnd, out rectangle);

            Rectangle clientRectangle;
            PInvoke.GetClientRect(hwnd, out clientRectangle);

            var x = Convert.ToInt32(rectangle.X + (clientRectangle.Width) * 0.5);
            var y = Convert.ToInt32(rectangle.Y + (clientRectangle.Height) * 0.5);

            PInvoke.SetForegroundWindow(hwnd);
            LeftClick(x, y);

            // First center view using Ctrl-Space
            PInvoke.keybd_event(Convert.ToByte(VK_LCONTROL), 0, KEYEVENTF_KEYDOWN, 0);
            PInvoke.keybd_event(Convert.ToByte(VK_SPACE), 0, KEYEVENTF_KEYDOWN, 0);
            Thread.Sleep(200);
            PInvoke.keybd_event(Convert.ToByte(VK_SPACE), 0, KEYEVENTF_KEYUP, 0);
            PInvoke.keybd_event(Convert.ToByte(VK_LCONTROL), 0, KEYEVENTF_KEYUP, 0);
            Thread.Sleep(200);
        }

        public static void SaveCustomView(IntPtr hwnd, string keybinding)
        {
            uint customViewKey = (uint)(Convert.ToInt32(keybinding) + KEY_0);

            PInvoke.SetForegroundWindow(hwnd);
            Thread.Sleep(500);

            PInvoke.SetFocus(hwnd);
            Thread.Sleep(300);

            // Set view using Ctrl-Alt-0
            PInvoke.keybd_event(Convert.ToByte(VK_LCONTROL), 0, KEYEVENTF_KEYDOWN, 0);
            PInvoke.keybd_event(Convert.ToByte(VK_LMENU), 0, KEYEVENTF_KEYDOWN, 0);
            PInvoke.keybd_event(Convert.ToByte(customViewKey), 0, KEYEVENTF_KEYDOWN, 0);
            Thread.Sleep(200);
            PInvoke.keybd_event(Convert.ToByte(customViewKey), 0, KEYEVENTF_KEYUP, 0);
            PInvoke.keybd_event(Convert.ToByte(VK_LMENU), 0, KEYEVENTF_KEYUP, 0);
            PInvoke.keybd_event(Convert.ToByte(VK_LCONTROL), 0, KEYEVENTF_KEYUP, 0);
        }

        public static void LoadCustomView(string keybinding)
        {
            var simualatorProcess = DiagnosticManager.GetSimulatorProcess();
            if (simualatorProcess != null)
            {
                // First center view to make sure recalling custom camera works on the first press
                InputEmulationManager.CenterView(simualatorProcess.Handle);

                uint customViewKey = (uint)(Convert.ToInt32(keybinding) + KEY_0);

                PInvoke.SetForegroundWindow(simualatorProcess.Handle);
                Thread.Sleep(300);

                PInvoke.SetFocus(simualatorProcess.Handle);
                Thread.Sleep(300);

                // First center view using Ctrl-Space
                PInvoke.keybd_event(Convert.ToByte(VK_LCONTROL), 0, KEYEVENTF_KEYDOWN, 0);
                PInvoke.keybd_event(Convert.ToByte(VK_SPACE), 0, KEYEVENTF_KEYDOWN, 0);
                Thread.Sleep(200);
                PInvoke.keybd_event(Convert.ToByte(VK_SPACE), 0, KEYEVENTF_KEYUP, 0);
                PInvoke.keybd_event(Convert.ToByte(VK_LCONTROL), 0, KEYEVENTF_KEYUP, 0);
                Thread.Sleep(200);

                // Then load view using Alt-0
                PInvoke.keybd_event(Convert.ToByte(VK_LMENU), 0, KEYEVENTF_KEYDOWN, 0);
                PInvoke.keybd_event(Convert.ToByte(customViewKey), 0, KEYEVENTF_KEYDOWN, 0);
                Thread.Sleep(200);
                PInvoke.keybd_event(Convert.ToByte(customViewKey), 0, KEYEVENTF_KEYUP, 0);
                PInvoke.keybd_event(Convert.ToByte(VK_LMENU), 0, KEYEVENTF_KEYUP, 0);

                Thread.Sleep(500);
            }
        }

        public static void ToggleFullScreenPanel(IntPtr hwnd)
        {
            PInvoke.SetForegroundWindow(hwnd);
            Thread.Sleep(500);

            PInvoke.SetFocus(hwnd);
            Thread.Sleep(300);

            PInvoke.keybd_event(Convert.ToByte(VK_RMENU), 0, KEYEVENTF_KEYDOWN, 0);
            PInvoke.keybd_event(Convert.ToByte(VK_ENT), 0, KEYEVENTF_KEYDOWN, 0);
            Thread.Sleep(200);
            PInvoke.keybd_event(Convert.ToByte(VK_ENT), 0, KEYEVENTF_KEYUP, 0);
            PInvoke.keybd_event(Convert.ToByte(VK_RMENU), 0, KEYEVENTF_KEYUP, 0);
        }

        public static void LeftClickReadyToFly()
        {
            var simualatorProcess = DiagnosticManager.GetSimulatorProcess();
            if (simualatorProcess != null)
            {
                var hwnd = simualatorProcess.Handle;

                PInvoke.SetForegroundWindow(hwnd);
                Thread.Sleep(250);

                Rectangle rectangle;
                PInvoke.GetWindowRect(hwnd, out rectangle);

                Rectangle clientRectangle;
                PInvoke.GetClientRect(hwnd, out clientRectangle);

                // The "Ready to Fly" button is at about 94.7% width, 84% to 96.25% height (91.3% default) with interface scaling of 70 at the lower right corner of game window
                // Try to click the area a few times to hit that button for both full screen and windows mode

                var x = Convert.ToInt32(rectangle.X + (clientRectangle.Width) * 0.947);
                var y = Convert.ToInt32(rectangle.Y + (clientRectangle.Height) * 0.84);
                LeftClick(x, y);
                LeftClick(x, y);

                for (var top = y; top < y + (clientRectangle.Height) * 0.125; top = top + 10)
                {
                    Debug.WriteLine($"Trying to click at x: {x}   y: {Convert.ToInt32(top)}");
                    PInvoke.SetCursorPos(x, Convert.ToInt32(top));
                    Thread.Sleep(100);
                    PInvoke.mouse_event(MOUSEEVENTF_LEFTDOWN, x, Convert.ToInt32(top), 0, 0);
                    Thread.Sleep(200);
                    PInvoke.mouse_event(MOUSEEVENTF_LEFTUP, x, Convert.ToInt32(top), 0, 0);
                    Thread.Sleep(200);
                    PInvoke.mouse_event(MOUSEEVENTF_LEFTDOWN, x, Convert.ToInt32(top), 0, 0);
                    Thread.Sleep(200);
                    PInvoke.mouse_event(MOUSEEVENTF_LEFTUP, x, Convert.ToInt32(top), 0, 0);
                }
            }
        }
    }
}