// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System.IO;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Implements file system for FMOD via a file on regular local FS used by download to incrementally write into
    /// </summary>
    public class DownloadFileSystemCachedFile : DownloadFileSystemBase
    {
        /// <summary>
        /// backing store
        /// </summary>
        FileStream fileStream = null;
        BinaryWriter writer = null;
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
                return (uint)this.fileStream.Length;
            }

            protected set
            {
                throw new System.NotImplementedException();
            }
        }
        public override uint available { get; protected set; }

        public DownloadFileSystemCachedFile(string filepath, uint _decoder_block_size, int starvingRetryCount, LogLevel logLevel)
            : base(_decoder_block_size, logLevel)
        {
            this.fileStream = new FileStream(filepath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            this.writer = new BinaryWriter(this.fileStream);
            this.maxTimeoutAttempts = starvingRetryCount;
        }
        public override void Write(byte[] bytes)
        {
            lock (this.bufferLock)
            {
                this.writer.Seek(0, SeekOrigin.End);
                this.writer.Write(bytes);
                this.available = (uint)this.writer.BaseStream.Length;
            }
        }

        public override byte[] Read(uint offset, uint toread, uint mediaLength)
        {
            // decoder initial seek/s
            if (offset > AudioStreamBase.INFINITE_LENGTH - this.decoder_block_size * 2)
                return new byte[0];

            // actual av *can* be < than requested if reading near end of the file
            long av = this.fileStream.Length - offset;
            var attempts = 0;
            while (av < toread
                && !this.shutting_down
                && ++attempts < this.maxTimeoutAttempts
                && mediaLength == AudioStreamBase.INFINITE_LENGTH
                )
            {
                Debug.LogFormat("Read underflow attempt {0} / {1}, offset: {2} toread: {3}, available: {4}", attempts, this.maxTimeoutAttempts, offset, toread, av);
                System.Threading.Thread.Sleep(DownloadFileSystemCachedFile.readTimeout);
                av = this.fileStream.Length - offset;
            }

            if (av < 1)
            {
                return new byte[0];
            }

            var result_size = (uint)Mathf.Min(av, toread);
            var result = new byte[result_size];

            lock (this.bufferLock)
            {
                this.fileStream.Seek(offset, SeekOrigin.Begin);
                this.fileStream.Read(result, 0, (int)result_size);
            }

            return result;

        }
        public override void CancelPendingRead()
        {
            this.shutting_down = true;
        }
        public override void CloseStore()
        {
            this.writer.Close();
            // TODO:
            // Dispose is protected on 3.5 runtime..
            // this.writer.Dispose();
            this.writer = null;

            this.fileStream.Close();
            this.fileStream.Dispose();
            this.fileStream = null;
        }
    }
}