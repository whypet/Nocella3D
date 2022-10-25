using System;

namespace Nocella3D.Parallelization;

public class FixedLambda<T> where T : unmanaged {
	public unsafe delegate void FixedAction(int i, T* ptr);

	public FixedAction Action { get; set; }

	public FixedLambda(FixedAction action) =>
		Action = action;

	public unsafe Action<int> Lambda(T* ptr) =>
		i => Action(i, ptr);
}