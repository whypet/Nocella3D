using System;
using System.Collections.Generic;

namespace Nocella3D.Virtual;

/// <summary>
/// A dynamic block of virtual memory optimized for big chunks of data that is resized as items are added to it.
/// </summary>
/// <typeparam name="T">The type to be used when accessing the memory block.</typeparam>
public unsafe class DynamicVirtualMemory<T> : VirtualMemory<T> where T : unmanaged {
	private readonly object reinitLock = new();

	private nint index;
	public nint Index {
		get {
			if (index >= Length)
				index = 0;
			return index;
		}
		private set => index = value;
	}

	public override T this[nint i] {
		get {
			if (i < 0 || i >= Length)
				throw new IndexOutOfRangeException();
			return Data[i];
		}
		set {
			if (i < 0)
				throw new IndexOutOfRangeException();
			while (i >= Length)
				IncreaseSize(i);
			Data[i] = value;
		}
	}

	/// <summary>
	/// The size alignment of the memory block.
	/// The size will always be a multiple of this value after resizing the memory block.
	/// Its default value is the common Windows page granularity, and it is recommended that it stays a multiple of the device's page granularity.
	/// </summary>
	public nint Alignment { get; set; } = VirtualMemory.PageGranularity;

	/// <summary>
	/// The amount of items in the array, incremented by the Add methods and decremented by the Remove methods.
	/// </summary>
	public nint Count { get; private set; }

	public DynamicVirtualMemory(nint source, nint size = 1, nint alignment = VirtualMemory.PageGranularity) :
		base(source, (size + alignment - 1) / alignment * alignment)
	{
		Alignment = alignment;
	}

	public DynamicVirtualMemory(VirtualMemory<T> source, nint? size = null) : base(source, size) { }
	public DynamicVirtualMemory(T[] source, nint? size = null) : base(source, size) { }
	public DynamicVirtualMemory(nint source, nint size) : base(source, size) { }
	public DynamicVirtualMemory(void* source, nint size) : base(source, size) { }

	private void ReallocateAndCopy(nint newSize) {
		lock (reinitLock) {
			nint tempSize = Math.Min(Size, newSize);

			using VirtualMemory<T> mem = new(tempSize);
			Copy(mem);

			Dispose(true);
			Initialize(newSize);

			mem.Copy(this);
		}
	}

	private void IncreaseSize(nint i = -1) {
		if (i < 0)
			i = Index;
		if (i >= Length)
			ReallocateAndCopy(Math.Max((Size + sizeof(T) + Alignment - 1) / Alignment * Alignment, Size + Alignment));
	}

	private void DecreaseSize(nint i = -1) {
		if (Size > Alignment && (Count - 1) * (Alignment + 1) / Alignment < Length - Alignment / sizeof(T))
			ReallocateAndCopy(Size - Alignment);
	}

	/// <summary>
	/// Reallocates the block of memory with a new size while keeping the original data.
	/// A new block of memory with the current size, or the desired size if it's smaller,
	/// will be created in the process.
	/// </summary>
	/// <param name="alignedSize">
	/// The new size of the block of memory.
	/// This value will be rounded up to a multiple of the alignment.
	/// </param>
	public void Resize(nint alignedSize) {
		alignedSize = (alignedSize + Alignment - 1) / Alignment * Alignment;
		ReallocateAndCopy(alignedSize);
	}

	/// <summary>
	/// Sets the item to the current index.
	/// If the current index hasn't been modified, the behavior of this method is to add the item after the last item.
	/// </summary>
	/// <param name="item">The item to add to the array.</param>
	public void Add(T item) {
		DecreaseSize(Index);

		Data[Index] = item;

		Index++;
		Count++;
	}

	/// <summary>
	/// Removes an item from the array and moves every other item by one index to the left.
	/// Warning: Very slow for big arrays!
	/// </summary>
	/// <param name="item">The item to look for in the array and remove.</param>
	/// <returns>A boolean value indicating whether the function succeeded.</returns>
	public bool Remove(T item) {
		long i = Find(item);
		if (i < 0)
			return false;

		for (; i < Length; i++) {
			if (EqualityComparer<T>.Default.Equals(Data[i], item)) {
				if (i + 1 < Length)
					Data[i] = Data[i + 1];
				else
					Data[i] = default;
			}
		}

		Index--;
		Count--;

		DecreaseSize(Index);

		return true;
	}

	/// <summary>
	/// Removes the last item from the array and returns it.
	/// </summary>
	/// <returns>The item that was removed from the array.</returns>
	public T Pop() {
		if (Index <= 0)
			throw new InvalidOperationException("The array is empty.");

		T item = Data[Index - 1];
		Data[Index - 1] = default;

		Index--;
		Count--;

		DecreaseSize(Index);

		return item;
	}

	~DynamicVirtualMemory() =>
	   Dispose(false);
}