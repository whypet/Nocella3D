using System;

public delegate void BufferSwapProcedure(nint pixelBuffer, nint bufferSize);

internal interface ISwapChain : IDisposable {
	BufferSwapProcedure BufferSwap { get; }
	nint BufferSize { get; }

	void Present();
}