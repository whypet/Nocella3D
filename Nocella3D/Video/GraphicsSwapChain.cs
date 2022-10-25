using Nocella3D.Virtual;
using Silk.NET.Maths;
using System;
using System.Threading;

namespace Nocella3D.Video;

public class GraphicsSwapChain : ISwapChain {
	private readonly CancellationTokenSource cancelSource = new();
	private readonly AutoResetEvent resetEvent = new(false);
	private readonly Thread bufferSwapThread;
	private bool disposed;

	internal VirtualMemory<uint>[] backBuffers;
	internal int backBufferIndex;

	public BufferSwapProcedure BufferSwap { get; }

	private Vector2D<int> imageSize;
	public  Vector2D<int> ImageSize {
		get => imageSize;
		set {
			imageSize = value;
			for (int i = 0; i < backBuffers.Length; i++) {
				backBuffers[i]?.Dispose();
				backBuffers[i] = new VirtualMemory<uint>(value.X * value.Y * sizeof(uint));
			}
		}
	}
	public nint BufferSize => ImageSize.X * ImageSize.Y * sizeof(uint);

	public GraphicsSwapChain(Vector2D<int> bufferSize, int bufferCount, BufferSwapProcedure bufferSwapProc) {
		backBuffers = new VirtualMemory<uint>[bufferCount];
		ImageSize  = bufferSize;

		BufferSwap = bufferSwapProc;
		bufferSwapThread = new Thread(() => {
			while (!cancelSource.IsCancellationRequested) {
				if (!resetEvent.WaitOne(1000))
					continue;

				BufferSwap(backBuffers[backBufferIndex].Address, (nint)backBuffers[backBufferIndex].Size);

				resetEvent.Set();
			}
		});
		bufferSwapThread.Start();
	}

	public void Present() {
		resetEvent.Set();
		resetEvent.WaitOne();

		backBufferIndex++;
		backBufferIndex %= backBuffers.Length;
	}

	protected virtual void Dispose(bool disposing) {
		if (!disposed) {
			if (disposing) {
				cancelSource.Cancel();

				for (int i = 0; i < backBuffers.Length; i++)
					backBuffers[i].Dispose();
			}

			disposed = true;
		}
	}

	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	~GraphicsSwapChain() =>
		Dispose(disposing: false);
}