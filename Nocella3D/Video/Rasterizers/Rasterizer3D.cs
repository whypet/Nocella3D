using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;

namespace Nocella3D.Video.Rasterizers;

public class Rasterizer3D : RasterizerBase {
	private unsafe delegate void FillTriangleDelegate(Vector2[] points, Vector4[] colors,
		Box2D<int> boundingBox, uint* colorBuffer, Vector2D<int> colorBufferSize);

	private unsafe FillTriangleDelegate fillTriangle = new(FillTriangleAvx2);

	public override unsafe bool SetAccelerationMode(RasterAcceleration accel) {
		switch (accel) {
			case RasterAcceleration.Avx2:
				fillTriangle = new FillTriangleDelegate(FillTriangleAvx2);
				return true;
			default:
				return false;
		}
	}

	internal override unsafe bool Rasterize(Memory<byte> vertexBuffer,
		int vertexStride, uint* colorBuffer, Vector2D<int> colorBufferSize)
	{
		if (Pipeline!.VertexShader == null || Pipeline!.PixelShader == null ||
			vertexBuffer.Length % vertexStride != 0)
		{
			return false;
		}

		int length = vertexBuffer.Length / vertexStride,
			size   = Pipeline!.VertexShader!.VertexSize * length;
		byte* vertices = stackalloc byte[size];

		for (int i = 0; i < length; i++) {
			Memory<byte> buffer = Pipeline!.VertexShader!.Process(
				vertexBuffer[(i * vertexStride)..((i + 1) * vertexStride)]);

			fixed (byte* ptr = buffer.Span) {
				Buffer.MemoryCopy(ptr, vertices + i * vertexStride,
					Pipeline!.VertexShader!.VertexSize, Pipeline!.VertexShader!.VertexSize);
			}
		}
		
		// Broken
		Thread[] threads = new Thread[length];

		for (int i = 0; i < threads.Length; i++) {
			threads[i] = new Thread(() =>
				RasterizeTriangle((nint)vertices + i * vertexStride,
					vertexStride, colorBuffer, colorBufferSize));
			threads[i].Start();
		}

		for (int i = 0; i < threads.Length; i++)
			threads[i].Join();

		return true;
	}

	private static unsafe void FillTriangleAvx2(Vector2[] points, Vector4[] colors,
		Box2D<int> boundingBox, uint* colorBuffer, Vector2D<int> colorBufferSize)
	{
		int xMin = Math.Max(boundingBox.Min.X, 0), yMin = Math.Max(boundingBox.Min.Y, 0),
			xMax = Math.Min(boundingBox.Max.X, colorBufferSize.X - 1),
			yMax = Math.Min(boundingBox.Max.Y, colorBufferSize.Y - 1),
			width = xMax - xMin, height = yMax - yMin;

		static Vector256<float> EdgeFunction(
			Vector2 a, Vector2 b, Vector256<float> x, Vector256<float> y)
		{
			Vector256<float>
				v1 = Vector256.Create(b.X - a.X),
				v2 = Avx.Subtract(y, Vector256.Create(a.Y)),
				v3 = Vector256.Create(b.Y - a.Y),
				v4 = Avx.Subtract(x, Vector256.Create(a.X));
			return Avx.Subtract(Avx.Multiply(v1, v2), Avx.Multiply(v3, v4));
		}
		
		static Vector256<float>[] CartesianToBarycentric(
			Vector2 a, Vector2 b, Vector2 c, Vector256<float> x, Vector256<float> y)
		{
			Vector256<float>
				v1 = Vector256.Create(b.Y - c.Y), v2 = Avx.Subtract(x, Vector256.Create(c.X)),
				v3 = Vector256.Create(c.X - b.X), v4 = Avx.Subtract(y, Vector256.Create(c.Y)),
				v5 = Vector256.Create(b.Y - c.Y), v6 = Vector256.Create(a.X - c.X),
				v7 = Vector256.Create(c.X - b.X), v8 = Vector256.Create(a.Y - c.Y),
				v9 = Vector256.Create(c.Y - a.Y);

			Vector256<float>
				m1 = Avx.Multiply(v1, v2), m2 = Avx.Multiply(v3, v4),
				m3 = Avx.Multiply(v5, v6), m4 = Avx.Multiply(v7, v8),
				m5 = Avx.Multiply(v9, v2), m6 = Avx.Multiply(v6, v4);

			Vector256<float> a2 = Avx.Add(m3, m4);
			Vector256<float> lambdaA = Avx.Divide(Avx.Add(m1, m2), a2);
			Vector256<float> lambdaB = Avx.Divide(Avx.Add(m5, m6), a2);

			return new Vector256<float>[] {
				lambdaA, lambdaB, Avx.Subtract(Avx.Subtract(Vector256.Create(1f), lambdaA), lambdaB)
			};
		}

		for (int i = 0; i < width * height / 8; i += 8) {
			int x = i % width + xMin, y = i / width + yMin;

			Vector256<float> x256 = Vector256.Create((float)x, x + 1, x + 2, x + 3, x + 4, x + 5, x + 6, x + 7),
				y256 = Vector256.Create((float)y),
				a = EdgeFunction(points[0], points[1], x256, y256),
				b = EdgeFunction(points[1], points[2], x256, y256),
				c = EdgeFunction(points[2], points[0], x256, y256);

			Vector256<uint> gate = Vector256<uint>.AllBitsSet;

			gate = Avx2.And(gate, Avx.CompareNotLessThan(a, Vector256<float>.Zero).AsUInt32());
			gate = Avx2.And(gate, Avx.CompareNotLessThan(b, Vector256<float>.Zero).AsUInt32());
			gate = Avx2.And(gate, Avx.CompareNotLessThan(c, Vector256<float>.Zero).AsUInt32());

			if (!Avx.TestZ(gate, gate)) {
				Vector256<float>[] mix = CartesianToBarycentric(points[0], points[1], points[2], x256, y256);

				Vector256<float>[] channels = {
					Avx.Add(Avx.Add(Avx.Multiply(Vector256.Create(colors[0].X), mix[0]),
						Avx.Multiply(Vector256.Create(colors[1].X), mix[1])),
						Avx.Multiply(Vector256.Create(colors[2].X), mix[2])),
					Avx.Add(Avx.Add(Avx.Multiply(Vector256.Create(colors[0].Y), mix[0]),
						Avx.Multiply(Vector256.Create(colors[1].Y), mix[1])),
						Avx.Multiply(Vector256.Create(colors[2].Y), mix[2])),
					Avx.Add(Avx.Add(Avx.Multiply(Vector256.Create(colors[0].Z), mix[0]),
						Avx.Multiply(Vector256.Create(colors[1].Z), mix[1])),
						Avx.Multiply(Vector256.Create(colors[2].Z), mix[2])),
					Avx.Add(Avx.Add(Avx.Multiply(Vector256.Create(colors[0].W), mix[0]),
						Avx.Multiply(Vector256.Create(colors[1].W), mix[1])),
						Avx.Multiply(Vector256.Create(colors[2].W), mix[2]))
				};

				Vector256<uint> pixel = Avx.ConvertToVector256Int32(
					Avx.Multiply(channels[0], Vector256.Create(255f))).AsUInt32();
				pixel = Avx2.Or(pixel, Avx2.ShiftLeftLogical(Avx.ConvertToVector256Int32(
					Avx.Multiply(channels[1], Vector256.Create(255f))).AsUInt32(), 8));
				pixel = Avx2.Or(pixel, Avx2.ShiftLeftLogical(Avx.ConvertToVector256Int32(
					Avx.Multiply(channels[2], Vector256.Create(255f))).AsUInt32(), 16));
				pixel = Avx2.Or(pixel, Avx2.ShiftLeftLogical(Avx.ConvertToVector256Int32(
					Avx.Multiply(channels[3], Vector256.Create(255f))).AsUInt32(), 24));

				uint* data = colorBuffer + (colorBufferSize.Y - 1 - y) * colorBufferSize.X + x;
				Vector256<uint> newPixel = Avx2.And(gate.AsUInt32(), pixel),
					oldPixel = Avx2.AndNot(gate.AsUInt32(), Avx.LoadVector256(data));
				Avx.Store(data, Avx2.Or(newPixel, oldPixel).AsUInt32());
			}
		}
	}

	private unsafe void RasterizeTriangle(nint vertexPtr, int vertexStride,
		uint* colorBuffer, Vector2D<int> colorBufferSize)
	{
		Vector3* posPtr = (Vector3*)(vertexPtr + Pipeline!.VertexIndexPosition);
		Vector3[] vertices = new Vector3[3];
		for (int i = 0; i < vertices.Length; i++)
			vertices[i] = *(Vector3*)((byte*)posPtr + i * vertexStride);
		
		Vector4* colorPtr = (Vector4*)(vertexPtr + Pipeline!.VertexIndexColor);
		Vector4[] colors = new Vector4[3];
		for (int i = 0; i < colors.Length; i++)
			colors[i] = *(Vector4*)((byte*)colorPtr + i * vertexStride);

		Vector2[] points = new Vector2[3];
		for (int i = 0; i < points.Length; i++) {
			Vector3 vertex = vertices[i] / vertices[i].Z;
			points[i] = new Vector2(vertex.X * colorBufferSize.X, vertex.Y * colorBufferSize.Y);
		}

		Box2D<int> boundingBox = new(
			(int)Math.Min(Math.Min(points[0].X, points[1].X), points[2].X),
			(int)Math.Min(Math.Min(points[0].Y, points[1].Y), points[2].Y),
			(int)Math.Ceiling(Math.Max(Math.Max(points[0].X, points[1].X), points[2].X)),
			(int)Math.Ceiling(Math.Max(Math.Max(points[0].Y, points[1].Y), points[2].Y)));

		fillTriangle(points, colors, boundingBox, colorBuffer, colorBufferSize);
	}
}
