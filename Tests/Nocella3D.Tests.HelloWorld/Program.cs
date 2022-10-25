using Nocella3D.Video;
using Nocella3D.Video.Rasterizers;
using Nocella3D.Windowing;
using Silk.NET.Maths;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

using static Windows.Win32.PInvoke;

namespace Nocella3D.Tests.HelloWorld;

[SupportedOSPlatform("windows5.1.2600")]
internal class Program {
	private static readonly nint bits;

	private static Win32Window? window;
	private static BufferSwapProcedure? bufferSwap;

	private static HDC        dcClient;
	private static CreatedHDC dcTemp;
	private static HBITMAP    bmTemp;
	private static BITMAPINFO bmi;

	private static unsafe void Initialize() {
		window = new(640, 480, "Nocella3D Hello World");
		window.Show();

		bmi = new BITMAPINFO() {
			bmiHeader = new BITMAPINFOHEADER() {
				biSize        = (uint)sizeof(BITMAPINFOHEADER),
				biWidth       = window.Size.X,
				biHeight      = window.Size.Y,
				biPlanes      = 1,
				biBitCount    = 32,
				biCompression = BI_COMPRESSION.BI_RGB
			}
		};

		dcClient = GetDC(new HWND(window.Handle));
		dcTemp   = CreateCompatibleDC(dcClient);
		_ = SetStretchBltMode(dcClient, STRETCH_BLT_MODE.STRETCH_DELETESCANS);

		fixed (BITMAPINFO* bmiPtr = &bmi)
		fixed (nint* bitsPtr = &bits)
			bmTemp = CreateDIBSection(dcClient, bmiPtr, DIB_USAGE.DIB_RGB_COLORS, (void**)bitsPtr, HANDLE.Null, 0);

		SelectObject(new HDC(dcTemp), new HGDIOBJ(bmTemp));

		bufferSwap = new BufferSwapProcedure((nint pixelBuffer, nint bufferSize) => {
			long size = bmi.bmiHeader.biWidth * bmi.bmiHeader.biHeight * bmi.bmiHeader.biBitCount / 8;
			Buffer.MemoryCopy((void*)pixelBuffer, (void*)bits, size, size);

			StretchBlt(dcClient, 0, 0, window.Size.X, window.Size.Y,
				new HDC(dcTemp), 0, 0, bmi.bmiHeader.biWidth, bmi.bmiHeader.biHeight, ROP_CODE.SRCCOPY);
		});
	}

	private static unsafe void Main() {
		Initialize();

		GraphicsDevice device = new();

		using GraphicsSwapChain swapChain = new(
			new Vector2D<int>(bmi.bmiHeader.biWidth, bmi.bmiHeader.biHeight), 2, bufferSwap!);
		device.SetSwapChain(swapChain);

		CommandQueue queue = device.CreateCommandQueue();
		GraphicsPipeline pipeline = new(
			new Rasterizer3D(), new BlitVertexShader(), new BlitPixelShader(),
			0, sizeof(Vector3));

		Span<VertexPositionColor> vertices = stackalloc VertexPositionColor[] {
			new(new Vector3(0.25f, 0.25f, 1f), new Vector4(1f, 0f, 0f, 1f)),
			new(new Vector3(0.75f, 0.25f, 1f), new Vector4(0f, 1f, 0f, 1f)),
			new(new Vector3(0.75f, 0.75f, 1f), new Vector4(0f, 0f, 1f, 1f)),

			new(new Vector3(0.25f, 0.25f, 1f), new Vector4(1f, 0f, 0f, 1f)),
			new(new Vector3(0.75f, 0.75f, 1f), new Vector4(0f, 0f, 1f, 1f)),
			new(new Vector3(0.25f, 0.75f, 1f), new Vector4(0f, 1f, 0f, 1f))
		};

		int size = vertices.Length * sizeof(VertexPositionColor);
		const int count = 20;
		Memory<byte> vertexBytes = new byte[size * count];

		fixed (VertexPositionColor* vertexStructPtr = vertices)
		fixed (byte* vertexBytePtr = vertexBytes.Span) {
			for (int i = 0; i < count; i++) {
				Buffer.MemoryCopy(vertexStructPtr,
					vertexBytePtr + i * size,
					vertexBytes.Length, size);
			}
		}

		int frames = 0;
		Stopwatch sw = Stopwatch.StartNew();

		while (window!.Exists) {
			queue.SetPipeline(pipeline);
			queue.UploadVertexBuffer(vertexBytes, sizeof(VertexPositionColor));
			queue.Rasterize();
			queue.Execute();

			swapChain.Present();
			window.PollEvents();

			frames++;
			if (sw.ElapsedMilliseconds >= 1000) {
				Console.WriteLine($"fps: {frames}");
				frames = 0;
				sw.Restart();
			}
		}

		Destroy();
	}

	private static void Destroy() {
		DeleteObject(new HGDIOBJ(bmTemp));
		DeleteDC(dcTemp);
		_ = ReleaseDC(new HWND(window!.Handle), dcClient);
		window.Dispose();
	}
}