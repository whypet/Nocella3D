using Nocella3D.Video;
using System;
using System.Numerics;

namespace Nocella3D.Tests.HelloWorld;

public struct VertexPositionColor {
	public Vector3 Position { get; set; }
	public Vector4 Color    { get; set; }

	public VertexPositionColor(Vector3 position, Vector4 color) =>
		(Position, Color) = (position, color);
}

public class BlitVertexShader : VertexShader {
	public override unsafe int VertexSize => sizeof(VertexPositionColor);

	public override Memory<byte> Process(Memory<byte> buffer) => buffer;
}

public class BlitPixelShader : PixelShader {
	public override unsafe Vector4 Process(Memory<byte> buffer) {
		fixed (byte* ptr = buffer.Span) {
			VertexPositionColor vertex = *(VertexPositionColor*)ptr;
			return vertex.Color;
		}
	}
}