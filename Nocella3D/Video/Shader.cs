using System;
using System.Numerics;

namespace Nocella3D.Video;

public abstract class VertexShader : IShader<Memory<byte>> {
	public abstract int VertexSize { get; }

	public abstract Memory<byte> Process(Memory<byte> buffer);
}

public abstract class PixelShader : IShader<Vector4> {
	public abstract Vector4 Process(Memory<byte> buffer);
}