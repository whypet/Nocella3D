using System;
using System.Collections.Generic;
using System.Linq;

namespace Nocella3D.Video;

public class CommandQueue {
	private readonly Queue<Action> commands = new();
	private readonly GraphicsCmdHandlers handlerList;

	internal CommandQueue(GraphicsCmdHandlers handlerList) =>
		this.handlerList = handlerList;

	public void SetPipeline(GraphicsPipeline pipeline) =>
		commands.Enqueue(() => handlerList.SetPipeline(pipeline));

	public void UploadVertexBuffer(Memory<byte> buffer, int stride) =>
		commands.Enqueue(() => handlerList.UploadVertexBuffer(buffer, stride));

	public void Rasterize() =>
		commands.Enqueue(() => handlerList.Rasterize());

	public void Execute() {
		while (commands.Any())
			commands.Dequeue()();
	}

	public void Clear() =>
		commands.Clear();
}