// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Implements file system for FMOD via memory buffer written into by download(handler), gets discarded as playback/FMOD reads progress
    /// </summary>
    public class DownloadFileSystemMemoryBuffer : DownloadFileSystemBase
    {
        /// <summary>
        /// backing store
        /// </summary>
        List<byte> buffer = new List<byte>();
        /// <summary>
        /// to offset reads after discarding previously read data
        /// </summary>
        uint bytesRemoved;
        /// <summary>
        /// Blocking read timeout on insufficient data
        /// Read can block up to (readTimeout * maxTimeoutAttempts) ms
        /// </summary>
        const int readTimeout = 100; // ms
        /// <summary>
        /// # of read attempts on insufficient read data
        /// </summary>
        readonly int maxTimeoutAttempts = 30;
        public override uint capacity
        {
            get
            {
                return (uint)this.buffer.Count;
            }

            protected set
            {
                throw new NotImplementedException();
            }
        }
        public override uint available { get; protected set; }

        public DownloadFileSystemMemoryBuffer(uint _decoder_block_size, int starvingRetryCount, LogLevel logLevel)
            : base(_decoder_block_size, logLevel)
        {
            this.maxTimeoutAttempts = starvingRetryCount;
        }

        public override void Write(byte[] bytes)
        {
            lock (this.bufferLock)
            {
                this.LOG(LogLevel.DEBUG, "Write: {0} b to {1}", bytes.Length, this.buffer.Count);
                this.buffer.AddRange(bytes);
            }
        }

        public override byte[] Read(uint offset, uint toread, uint mediaLength)
        {
            // decoder initial seek/s
            if (offset > AudioStreamBase.INFINITE_LENGTH - this.decoder_block_size * 2)
                return new byte[0];

            // adjust shift the offset based on how much was discarded so far
            offset -= this.bytesRemoved;

            // actual av *can* be < than requested if reading near end of the file
            long av = this.buffer.Count - offset;
            var attempts = 0;

            this.LOG(LogLevel.DEBUG, "Read offset: {0}, toread: {1}, av: {2}, buffer: {3} b", offset, toread, av, this.buffer.Count);

            while (av < toread
                && !this.shutting_down
                && ++attempts < this.maxTimeoutAttempts
                && mediaLength == AudioStreamBase.INFINITE_LENGTH
                )
            {
                this.LOG(LogLevel.INFO, "Read underflow attempt {0} / {1}, offset: {2} toread: {3}, buffer size: {4}, available: {5}"
                    , attempts, this.maxTimeoutAttempts, offset, toread, this.buffer.Count, av
                    );

                System.Threading.Thread.Sleep(DownloadFileSystemMemoryBuffer.readTimeout);
                av = this.buffer.Count - offset;
            }

            if (av < 1)
            {
                // if buffer/download is not progressing, offset is past the whole buffer
                // so at least try to stop getting them further apart once it hopefully catches up
                // but FMOD should stop really 
                System.Threading.Thread.Sleep(DownloadFileSystemMemoryBuffer.readTimeout);

                this.bytesRemoved += offset;
                this.LOG(LogLevel.DEBUG, "Discarded: {0}, discarded total: {1}, new size: {2}", offset, this.bytesRemoved, this.buffer.Count);

                return new byte[0];
            }

            this.available = (uint)av;

            var result_size = (uint)Mathf.Min(this.available, toread);
            var result = new byte[result_size];

            lock (this.bufferLock)
                Array.Copy(this.buffer.ToArray(), offset, result, 0, result_size);

            // if 'enough' was read, discard previously played data in the buffer
            // allocated == (offset + toread) + whatever was downloaded up to that point
            // ~ 10MB + dl
            // should hopefully cover all cases such as album artwork etc.
            // TODO: make this size configurable
            const uint dlCeiling = 1024*1024*10
                ;
            if (offset + toread > dlCeiling)
            {
                lock (this.bufferLock)
                    this.buffer.RemoveRange(0, (int)offset);

                this.bytesRemoved += offset;
                this.LOG(LogLevel.DEBUG, "Discarded: {0}, discarded total: {1}, new size: {2}", offset, this.bytesRemoved, this.buffer.Count);
            }

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