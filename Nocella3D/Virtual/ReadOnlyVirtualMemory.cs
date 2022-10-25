using System;
using System.IO.MemoryMappedFiles;

namespace Nocella3D.Virtual;

public unsafe class ReadOnlyVirtualMemory<T> : VirtualMemory<T> where T : unmanaged {
	public override T this[nint i] {
		get {
			if (i < 0 || i >= Length)
				throw new IndexOutOfRangeException();
			return Data[i];
		}
	}

	public ReadOnlyVirtualMemory(nint size) : base(size, MemoryMappedFileAccess.Read) { }
	public ReadOnlyVirtualMemory(VirtualMemory<T> source, nint? size = null) :
		base(source, size, MemoryMappedFileAccess.Read) { }
	public ReadOnlyVirtualMemory(T[] source, nint? size = null) :
		base(source, size, MemoryMappedFileAccess.Read) { }
	public ReadOnlyVirtualMemory(nint source, nint size) :
		base(source, size, MemoryMappedFileAccess.Read) { }
	public ReadOnlyVirtualMemory(void* source, nint size) :
		base(source, size, MemoryMappedFileAccess.Read) { }
}