﻿using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Screna.Native
{
    static class User32
    {
        const string DllName = "user32.dll";

        [DllImport(DllName)]
        public static extern IntPtr GetDesktopWindow();

        [DllImport(DllName)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport(DllName, SetLastError = true)]
        public static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport(DllName)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport(DllName)]
        public static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport(DllName)]
        public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);

        [DllImport(DllName)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SetWindowPositionFlags wFlags);

        [DllImport(DllName, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, [Out] StringBuilder lpString, int nMaxCount);
        
        [DllImport(DllName)]
        public static extern WindowStyles GetWindowLong(IntPtr hWnd, GetWindowLongValue nIndex);

        [DllImport(DllName)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport(DllName)]
        public static extern bool GetCursorInfo(out CursorInfo pci);

        [DllImport(DllName)]
        public static extern bool GetIconInfo(IntPtr hIcon, out IconInfo piconinfo);

        [DllImport(DllName)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport(DllName)]
        public static extern IntPtr GetWindow(IntPtr hWnd, GetWindowEnum uCmd);

        [DllImport(DllName)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport(DllName, SetLastError = true)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport(DllName, SetLastError = true)]
        public static extern bool GetCursorPos(ref Point lpPoint);

        [DllImport(DllName)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport(DllName)] 
        public static extern IntPtr GetWindowDC(IntPtr hWnd); 

        [DllImport(DllName, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport(DllName, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern HookProcedureHandle SetWindowsHookEx(int idHook, HookProcedure lpfn, IntPtr hMod, int dwThreadId);

        [DllImport(DllName, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int UnhookWindowsHookEx(IntPtr idHook);

        [DllImport(DllName, CharSet = CharSet.Auto)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport(DllName, CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        [DllImport(DllName)]
        public static extern int GetDoubleClickTime();

        //values from Winuser.h in Microsoft SDK.
        public const byte VK_SHIFT = 0x10;

        //may be possible to use these aggregates instead of L and R separately (untested)
        public const byte VK_CONTROL = 0x11,
            VK_MENU = 0x12,
            VK_PACKET = 0xE7;

        //Used to pass Unicode characters as if they were keystrokes. The VK_PACKET key is the low word of a 32-bit Virtual Key value used for non-keyboard input methods
        static int lastVirtualKeyCode,
            lastScanCode;
        static byte[] lastKeyState = new byte[255];
        static bool lastIsDead;

        public static void TryGetCharFromKeyboardState(int virtualKeyCode, int scanCode, int fuState, out char[] chars)
        {
            var dwhkl = GetActiveKeyboard(); //get the active keyboard layout
            TryGetCharFromKeyboardState(virtualKeyCode, scanCode, fuState, dwhkl, out chars);
        }

        public static void TryGetCharFromKeyboardState(int virtualKeyCode, int scanCode, int fuState, IntPtr dwhkl, out char[] chars)
        {
            var pwszBuff = new StringBuilder(64);
            var keyboardState = KeyboardState.GetCurrent();
            var currentKeyboardState = keyboardState.GetNativeState();
            var isDead = false;

            if (keyboardState.IsDown(Keys.ShiftKey))
                currentKeyboardState[(byte)Keys.ShiftKey] = 0x80;

            if (keyboardState.IsToggled(Keys.CapsLock))
                currentKeyboardState[(byte)Keys.CapsLock] = 0x01;

            var relevantChars = ToUnicodeEx(virtualKeyCode, scanCode, currentKeyboardState, pwszBuff, pwszBuff.Capacity, fuState, dwhkl);

            switch (relevantChars)
            {
                case -1:
                    isDead = true;
                    ClearKeyboardBuffer(virtualKeyCode, scanCode, dwhkl);
                    chars = null;
                    break;

                case 0:
                    chars = null;
                    break;

                case 1:
                    chars = pwszBuff.Length > 0 ? new[] { pwszBuff[0] } : null;
                    break;

                // Two or more (only two of them is relevant)
                default:
                    chars = pwszBuff.Length > 1 ? new[] { pwszBuff[0], pwszBuff[1] } : new[] { pwszBuff[0] };
                    break;
            }

            if (lastVirtualKeyCode != 0 && lastIsDead)
            {
                if (chars == null)
                    return;

                var sbTemp = new StringBuilder(5);
                ToUnicodeEx(lastVirtualKeyCode, lastScanCode, lastKeyState, sbTemp, sbTemp.Capacity, 0, dwhkl);
                lastIsDead = false;
                lastVirtualKeyCode = 0;

                return;
            }

            lastScanCode = scanCode;
            lastVirtualKeyCode = virtualKeyCode;
            lastIsDead = isDead;
            lastKeyState = (byte[])currentKeyboardState.Clone();
        }

        static void ClearKeyboardBuffer(int vk, int sc, IntPtr hkl)
        {
            var sb = new StringBuilder(10);

            int rc;

            do
            {
                var lpKeyStateNull = new byte[255];
                rc = ToUnicodeEx(vk, sc, lpKeyStateNull, sb, sb.Capacity, 0, hkl);
            }
            while (rc < 0);
        }

        static IntPtr GetActiveKeyboard()
        {
            var hActiveWnd = GetForegroundWindow(); //handle to focused window
            int dwProcessId;
            var hCurrentWnd = GetWindowThreadProcessId(hActiveWnd, out dwProcessId);
            //thread of focused window
            return GetKeyboardLayout(hCurrentWnd); //get the layout identifier for the thread whose window is focused
        }

        [DllImport(DllName)]
        public static extern int ToUnicodeEx(int wVirtKey,
            int wScanCode,
            byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)] StringBuilder pwszBuff,
            int cchBuff,
            int wFlags,
            IntPtr dwhkl);

        [DllImport(DllName)]
        public static extern int GetKeyboardState(byte[] pbKeyState);

        [DllImport(DllName, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern short GetKeyState(int vKey);

        [DllImport(DllName, CharSet = CharSet.Auto)]
        public static extern IntPtr GetKeyboardLayout(int dwLayout);
    }
}
