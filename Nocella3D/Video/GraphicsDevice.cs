using Nocella3D.Virtual;
using System;

namespace Nocella3D.Video;

public unsafe class GraphicsDevice {
	private readonly GraphicsCmdHandlers cmdHandlers;

	private GraphicsSwapChain? swapChain;
	private GraphicsPipeline?  pipeline;

	private Memory<byte> vertexBuffer;
	private int vertexStride;

	public GraphicsDevice() {
		cmdHandlers = new GraphicsCmdHandlers() {
			SetPipeline     = new GraphicsCmdHandlers.SetPipelineDelegate(SetPipeline),
			UploadVertexBuffer = new GraphicsCmdHandlers.UploadVertexBufferDelegate(UploadVertexBuffer),
			Rasterize       = new GraphicsCmdHandlers.RasterizeDelegate(Rasterize)
		};
	}

	private void SetPipeline(GraphicsPipeline pipeline) =>
		this.pipeline = pipeline;

	private void UploadVertexBuffer(Memory<byte> buffer, int stride) =>
		(vertexBuffer, vertexStride) = (buffer, stride);

	private bool Rasterize() {
		if (swapChain == null)
			return false;

		VirtualMemory<uint> buffer = swapChain.backBuffers[swapChain.backBufferIndex];
		return pipeline!.Rasterizer.Rasterize(
			vertexBuffer, vertexStride, buffer.Data, swapChain.ImageSize);
	}

	public CommandQueue CreateCommandQueue() => new(cmdHandlers);

	public void SetSwapChain(GraphicsSwapChain swapChain) =>
		this.swapChain = swapChain;
}