using static Windows.Win32.PInvoke;

using Silk.NET.Maths;
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using System.Threading;
using System.ComponentModel;

namespace Nocella3D.Windowing;

[SupportedOSPlatform("windows5.1.2600")]
public sealed unsafe class Win32Window : IWindow {
	private readonly WNDPROC         windowProc;
	private readonly MONITORENUMPROC monitorProc;
	private readonly string    className;
	private readonly HINSTANCE instance;

	private Point cursorPos   = new();
	private RECT  monitorRect = new();

	private bool disposed;

	public Vector2D<int> Size {
		get {
			GetClientRect(new HWND(Handle), out RECT rect);
			return new Vector2D<int>(rect.right - rect.left, rect.bottom - rect.top);
		}
		set => SetWindowPos(new HWND(Handle), HWND.Null, 0, 0, value.X, value.Y,
			SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
	}

	public nint Handle { get; }

	private bool exists = true;
	public  bool Exists => exists && IsWindow(new HWND(Handle));

	public event Action<Vector2D<int>>? SizeChanged;

	public unsafe Win32Window(int width, int height, string title) {
		instance = GetModuleHandle(new PCWSTR());

		windowProc  = new WNDPROC(WindowProc);
		monitorProc = new MONITORENUMPROC(MonitorEnumProc);

		fixed (char* className = $"{nameof(Nocella3D)}_{Environment.CurrentManagedThreadId:X8}") {
			this.className = new string(className);

			WNDCLASSEXW wndClassEx = new() {
				cbSize        = (uint)Unsafe.SizeOf<WNDCLASSEXW>(),
				lpfnWndProc   = windowProc,
				hInstance     = instance,
				lpszClassName = className,
				hCursor       = new HCURSOR(LoadImage(HINSTANCE.Null, IDC_ARROW,
					GDI_IMAGE_TYPE.IMAGE_CURSOR, 0, 0, IMAGE_FLAGS.LR_SHARED).Value)
			};

			if (RegisterClassEx(wndClassEx) == 0)
				throw new Win32Exception("Failed to register window class! Atom returned by RegisterClassEx is zero.");

			fixed (char* windowName = title) {
				if ((Handle = CreateWindowEx(WINDOW_EX_STYLE.WS_EX_OVERLAPPEDWINDOW,
					className, windowName, WINDOW_STYLE.WS_OVERLAPPEDWINDOW,
					0, 0, width, height, HWND.Null, HMENU.Null, instance, (void*)0)) == 0)
				{
					throw new Win32Exception("Failed to create window! Window handle returned by CreateWindowEx is zero.");
				}
			}
		}

		GetWindowRect(new HWND(Handle), out RECT windowRect);
		GetClientRect(new HWND(Handle), out RECT clientRect);

		int borderWidth  = windowRect.right  - windowRect.left - (clientRect.right  - clientRect.left),
			borderHeight = windowRect.bottom - windowRect.top  - (clientRect.bottom - clientRect.top);

		GetCursorPos(out cursorPos);
		EnumDisplayMonitors(HDC.Null, (RECT*)0, monitorProc, 0);

		SetWindowPos(new HWND(Handle), HWND.Null,
			monitorRect.left + (monitorRect.right  - monitorRect.left) / 2 - width  / 2 - borderWidth  / 2,
			monitorRect.top  + (monitorRect.bottom - monitorRect.top)  / 2 - height / 2 - borderHeight / 2,
			width + borderWidth, height + borderHeight, 0);
	}

	private LRESULT WindowProc(HWND windowHandle, uint message, WPARAM wParam, LPARAM lParam) {
		switch (message) {
			default:
				return DefWindowProc(windowHandle, message, wParam, lParam);
			case WM_SIZE:
				SizeChanged?.Invoke(new Vector2D<int>((int)lParam & 0xFFFF, (int)lParam >> 8 & 0xFFFF));
				return new LRESULT(0);
			case WM_CLOSE:
			case WM_QUIT:
				DestroyWindow(new HWND(Handle));
				return new LRESULT(0);
			case WM_DESTROY:
				PostQuitMessage(0);

				fixed (char* className = this.className)
					UnregisterClass(className, instance);

				exists = false;
				return new LRESULT(0);
		}
	}

	private unsafe BOOL MonitorEnumProc(HMONITOR monitorHandle, HDC deviceContext, RECT* clip, LPARAM lParam) {
		monitorRect = *clip;

		return !(monitorRect.left   < cursorPos.X && monitorRect.top     < cursorPos.Y &&
			     monitorRect.right >= cursorPos.X && monitorRect.bottom >= cursorPos.Y);
	}

	public void Show() => ShowWindow(new HWND(Handle), SHOW_WINDOW_CMD.SW_SHOW);
	public void Hide() => ShowWindow(new HWND(Handle), SHOW_WINDOW_CMD.SW_HIDE);

	public unsafe void PollEvents() {
		while (PeekMessage(out MSG message, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE)) {
			TranslateMessage(message);
			DispatchMessage(message);
		}
	}

	public void Dispose() {
		if (!disposed) {
			if (exists)
				DestroyWindow(new HWND(Handle));
			disposed = true;
		}

		GC.SuppressFinalize(this);
	}

	~Win32Window() =>
		Dispose();
}