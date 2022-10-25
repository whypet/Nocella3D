using System;

namespace Nocella3D.Video;

 struct GraphicsCmdHandlers {
	public delegate void SetPipelineDelegate(GraphicsPipeline pipeline);
	public delegate void UploadVertexBufferDelegate(Memory<byte> buffer, int stride);
	public delegate bool RasterizeDelegate();

	public SetPipelineDelegate SetPipeline { get; set; }
	public UploadVertexBufferDelegate UploadVertexBuffer { get; set; }
	public RasterizeDelegate Rasterize { get; set; }
}