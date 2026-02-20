// Packages/com.protosystem.core/Runtime/Capture/ReplayBuffer.cs
#if UNITY_EDITOR
using System;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Кольцевой буфер JPEG-кадров для replay buffer.
    /// Хранит последние N секунд видео в сжатом виде.
    /// </summary>
    public class ReplayBuffer : IDisposable
    {
        private readonly byte[][] _frames;
        private readonly int _capacity;
        private readonly int _fps;
        private int _writeIndex;
        private int _count;
        private int _lastWidth;
        private int _lastHeight;
        private bool _disposed;

        public int Count => _count;
        public int Capacity => _capacity;
        public int Fps => _fps;
        public float BufferUsagePercent => _capacity > 0 ? (float)_count / _capacity * 100f : 0f;

        public long EstimatedMemoryBytes
        {
            get
            {
                long total = 0;
                for (int i = 0; i < _count; i++)
                {
                    int idx = (_writeIndex - _count + i + _capacity) % _capacity;
                    if (_frames[idx] != null)
                        total += _frames[idx].Length;
                }
                return total;
            }
        }

        public ReplayBuffer(int fps, int bufferSeconds)
        {
            _fps = fps;
            _capacity = fps * bufferSeconds;
            _frames = new byte[_capacity][];
            _writeIndex = 0;
            _count = 0;
        }

        /// <summary>
        /// Добавить кадр из Texture2D (сжимает в JPEG).
        /// </summary>
        public void PushFrame(Texture2D texture, int jpegQuality)
        {
            if (_disposed) return;

            _lastWidth = texture.width;
            _lastHeight = texture.height;

            byte[] jpg = texture.EncodeToJPG(jpegQuality);
            _frames[_writeIndex] = jpg;
            _writeIndex = (_writeIndex + 1) % _capacity;
            if (_count < _capacity)
                _count++;
        }

        /// <summary>
        /// Добавить уже сжатый JPEG-кадр.
        /// </summary>
        public void PushFrame(byte[] jpgData, int width, int height)
        {
            if (_disposed) return;

            _lastWidth = width;
            _lastHeight = height;

            _frames[_writeIndex] = jpgData;
            _writeIndex = (_writeIndex + 1) % _capacity;
            if (_count < _capacity)
                _count++;
        }

        /// <summary>
        /// Обход кадров в хронологическом порядке.
        /// </summary>
        public void ReadFramesInOrder(Action<int, byte[]> callback)
        {
            if (_disposed || _count == 0) return;

            int startIdx = (_writeIndex - _count + _capacity) % _capacity;
            for (int i = 0; i < _count; i++)
            {
                int idx = (startIdx + i) % _capacity;
                if (_frames[idx] != null)
                    callback(i, _frames[idx]);
            }
        }

        /// <summary>
        /// Размеры последнего добавленного кадра.
        /// </summary>
        public (int width, int height) GetFrameDimensions()
        {
            return (_lastWidth, _lastHeight);
        }

        public void Clear()
        {
            for (int i = 0; i < _capacity; i++)
                _frames[i] = null;
            _writeIndex = 0;
            _count = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }
    }
}
#endif
