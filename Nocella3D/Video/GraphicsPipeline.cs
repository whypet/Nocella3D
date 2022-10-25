namespace Nocella3D.Video;

public class GraphicsPipeline {
	public RasterizerBase Rasterizer { get; }

	public VertexShader VertexShader { get; }
	public PixelShader  PixelShader  { get; }

	public nint VertexIndexPosition { get; set; }
	public nint VertexIndexColor    { get; set; }

	public GraphicsPipeline(RasterizerBase rasterizer, VertexShader vertexShader, PixelShader pixelShader) {
		(Rasterizer, VertexShader, PixelShader) = (rasterizer, vertexShader, pixelShader);
		Rasterizer.Pipeline = this;
	}

	public GraphicsPipeline(RasterizerBase rasterizer, VertexShader vertexShader, PixelShader pixelShader,
		nint vertexIndexPosition, nint vertexIndexColor) : this(rasterizer, vertexShader, pixelShader) =>
		(VertexIndexPosition, VertexIndexColor) = (vertexIndexPosition, vertexIndexColor);
}