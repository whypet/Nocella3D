using System;
using System.Threading;

namespace Nocella3D.Video;

public abstract class MultithreadedRasterizer : RasterizerBase {
	private Thread[] threads = Array.Empty<Thread>();

	public int MaxThreadCount { get; private set; }

	public bool SetMaxThreadCount(int count) {
		if (count <= 0)
			return false;
		else {
			MaxThreadCount = count;
			return true;
		}
	}

	public void RunRasterizerThreads(int faceCount, Action<int> rasterizeAction) {
		float threadCount = faceCount;
		while (threadCount > MaxThreadCount)
			threadCount /= 2f;

		threads = new Thread[(int)Math.Ceiling(threadCount)];

		for (int i = 0; i < threads.Length; i++) {
			threads[i] = new Thread(() => {
				int end = threads.Length / faceCount;
				end = Math.Min(end, faceCount - (int)(i * faceCount / threadCount));

				for (int j = 0; j < end; j++) {
					rasterizeAction(j + i * faceCount);
				}
			});
		}
		for (int i = 0; i < threads.Length; i++)
			threads[i].Start();
		for (int i = 0; i < threads.Length; i++)
			threads[i].Join();
	}
}