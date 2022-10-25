using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;

namespace Nocella3D.Virtual;

public static class VirtualMemory {
	/// <summary>
	/// The value of the page granularity commonly found on Windows devices.
	/// This will often be enough, as other operating systems will rather use a lower value being an exponent to 2, such as 4KB.
	/// </summary>
	public const nint PageGranularity = ushort.MaxValue + 1;
}

/// <summary>
/// A fast block of virtual memory optimized for big chunks of data.
/// </summary>
/// <typeparam name="T">The type to be used when accessing the memory block.</typeparam>
public unsafe class VirtualMemory<T> : IDisposable where T : unmanaged {
	private readonly MemoryMappedFileAccess access;
	private MemoryMappedFile mapping;
	private MemoryMappedViewAccessor accessor;

	/// <summary>
	/// The address of the data in memory.
	/// </summary>
	public nint Address { get; private set; }

	/// <summary>
	/// The pointer to the data in memory.
	/// </summary>
	public T* Data => (T*)Address;

	/// <summary>
	/// The size of the data.
	/// </summary>
	public nint Size { get; private set; }

	/// <summary>
	/// The length of the data, being its size divided by the size of its type.
	/// </summary>
	public nint Length => Size / sizeof(T);

	/// <summary>
	/// A copy of the data stored in a managed .NET array.
	/// </summary>
	public T[] Array {
		get {
			T[] array = new T[Length];
			fixed (T* dst = array)
				Buffer.MemoryCopy(Data, dst, Size, Size);
			return array;
		}
	}

	public virtual T this[nint i] {
		get {
			if (i < 0 || i >= Length)
				throw new IndexOutOfRangeException();
			return Data[i];
		}
		set {
			if (i < 0 || i >= Length)
				throw new IndexOutOfRangeException();
			Data[i] = value;
		}
	}

	public MemoryMappedViewStream Stream { get; private set; }

	public VirtualMemory(nint size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite) {
		this.access = access;
		mapping = MemoryMappedFile.CreateNew(null, size, access);
		accessor = mapping.CreateViewAccessor();
		Stream = mapping.CreateViewStream();
		Initialize(size, mapping, accessor, Stream);
	}

	private protected void Initialize(nint size,
		MemoryMappedFile? mapping = null,
		MemoryMappedViewAccessor? accessor = null,
		MemoryMappedViewStream? stream = null) {
		if (mapping == null)
			this.mapping = MemoryMappedFile.CreateNew(null, size, access);
		if (accessor == null)
			this.accessor = this.mapping.CreateViewAccessor();
		if (stream == null)
			Stream = this.mapping.CreateViewStream();

		Address = this.accessor.SafeMemoryMappedViewHandle.DangerousGetHandle();
		Size = size;
	}

	public VirtualMemory(VirtualMemory<T> source, nint? size = null,
		MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWriteExecute) :
		this(size ?? source.Size, access)
	{
		source.Copy(this, size);
	}

	public VirtualMemory(T[] source, nint? size = null,
		MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWriteExecute) :
		this(size ?? (source.Length * sizeof(T)), access)
	{
		fixed (T* src = source)
			Buffer.MemoryCopy(src, Data, Size, size ?? (source.Length * sizeof(T)));
	}

	public VirtualMemory(nint source, nint size,
		MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWriteExecute) :
		this(size, access)
	{
		if (source != 0)
			Buffer.MemoryCopy((void*)source, Data, Size, size);
	}

	public VirtualMemory(void* source, nint size,
		MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWriteExecute) :
		this(size, access)
	{
		Buffer.MemoryCopy(source, Data, Size, size);
	}

	public void Copy(VirtualMemory<T> destination, nint? size = null) =>
		Buffer.MemoryCopy(Data, destination.Data, size ?? destination.Size, Size);

	public void Copy(T[] destination, nint? size = null) {
		fixed (T* dst = destination)
			Buffer.MemoryCopy(Data, dst, size ?? (destination.Length * sizeof(T)), Size);
	}

	public void Copy(nint destination, nint size) =>
		Buffer.MemoryCopy(Data, (void*)destination, size, Size);

	public void Copy(void* destination, nint size) =>
		Buffer.MemoryCopy(Data, destination, size, Size);

	/// <summary>
	/// Retrieves the index of an item in the array.
	/// </summary>
	/// <param name="item">The item to look for in the array.</param>
	/// <remarks>Very slow for big arrays!</remarks>
	/// <returns>The index of the item in the array, or -1 if it hasn't been found.</returns>
	public nint Find(T item) {
		for (nint i = 0; i < Length; i++)
			if (EqualityComparer<T>.Default.Equals(Data[i], item))
				return i;
		return -1;
	}

	/// <summary>
	/// Retrieves the index of an item in the array using a predicate.
	/// </summary>
	/// <param name="predicate">The predicate to compare with items in the array.</param>
	/// <remarks>Very slow for big arrays!</remarks>
	/// <returns>The index of the item in the array, or -1 if it hasn't been found.</returns>
	public nint Find(Predicate<T> predicate) {
		for (nint i = 0; i < Length; i++)
			if (predicate(Data[i]))
				return i;
		return -1;
	}

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing) {
		if (disposing) {
			Stream.Dispose();
			accessor.Dispose();
			mapping.Dispose();
		}
	}

	~VirtualMemory() =>
	   Dispose(false);
}