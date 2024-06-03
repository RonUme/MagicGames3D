// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Implements file system for FMOD over a memory backed audio data / filled by UWR with content of a (local) file /
    /// </summary>
    public class LocalFileSystemMemoryBuffer : DownloadFileSystemBase
    {
        /// <summary>
        /// backing store
        /// </summary>
        List<byte> buffer = new List<byte>();
        public override uint capacity { get => (uint)this.buffer.Count; protected set => throw new System.NotImplementedException(); }
        public override uint available { get => (uint)this.buffer.Count; protected set => throw new System.NotImplementedException(); }

        public LocalFileSystemMemoryBuffer(uint _decoder_block_size, LogLevel logLevel)
            : base(_decoder_block_size, logLevel)
        {
        }

        public override void Write(byte[] bytes)
        {
            lock (this.bufferLock)
            {
                this.buffer.AddRange(bytes);
            }
        }

        public override byte[] Read(uint offset, uint toread, uint mediaLength)
        {
            // actual av can be < than requested when reading near the end of the file
            long av = this.buffer.Count - offset;

            if (av < 1)
            {
                return new byte[0];
            }

            var result_size = (uint)Mathf.Min(av, toread);
            var result = new byte[result_size];

            lock (this.bufferLock)
                Array.Copy(this.buffer.ToArray(), offset, result, 0, result_size);

            return result;
        }
        public override void CancelPendingRead()
        {
            this.shutting_down = true;
        }
        public override void CloseStore()
        {
            this.buffer.Clear();
        }
    }
}