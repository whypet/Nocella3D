using Silk.NET.Maths;
using System;

namespace Nocella3D.Windowing;

public interface IWindow : IDisposable {
	Vector2D<int> Size { get; set; }
	nint Handle { get; }
	bool Exists { get; }

	event Action<Vector2D<int>>? SizeChanged;

	void Show();
	void Hide();
	void PollEvents();
}