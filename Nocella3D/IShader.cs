using System;

namespace Nocella3D;

public interface IShader<T> {
    T Process(Memory<byte> buffer);
}