using Silk.NET.Maths;
using System;

namespace Nocella3D.Video;

public abstract class RasterizerBase {
	internal GraphicsPipeline? Pipeline { get; set; }

	public abstract bool SetAccelerationMode(RasterAcceleration accel);

	internal abstract unsafe bool Rasterize(Memory<byte> vertexBuffer,
		int vertexStride, uint* colorBuffer, Vector2D<int> colorBufferSize);
}