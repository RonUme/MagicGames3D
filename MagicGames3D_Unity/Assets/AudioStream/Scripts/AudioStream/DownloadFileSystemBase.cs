// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;

namespace AudioStream
{
    /// <summary>
    /// Implementation of a filesystem for FMOD
    /// </summary>
    public abstract class DownloadFileSystemBase
    {
        /// <summary>
        /// Currently allocated
        /// </summary>
        public abstract uint capacity { get; protected set; }
        /// <summary>
        /// Available for FMOD reads (for playback)
        /// </summary>
        public abstract uint available { get; protected set; }
        /// <summary>
        /// just for max EOF warning atm
        /// </summary>
        protected uint decoder_block_size;
        /// <summary>
        /// signal for blocking read to give up
        /// </summary>
        protected bool shutting_down = false;
        /// <summary>
        /// read/write lock
        /// </summary>
        protected readonly object bufferLock = new object();
        /// <summary>
        /// 
        /// </summary>
        protected LogLevel logLevel = LogLevel.ERROR;
        public DownloadFileSystemBase(uint _decoder_block_size, LogLevel _logLevel)
        {
            this.decoder_block_size = _decoder_block_size;
            this.logLevel = _logLevel;
        }
        /// <summary>
        /// Write content (via download handler)
        /// </summary>
        /// <param name="bytes"></param>
        public abstract void Write(byte[] bytes);
        /// <summary>
        /// Called by FMOD - always try to satisfy toread in blocking manner (for inf. streamed data), unless it's shutting down
        /// </summary>
        /// <param name="toread"></param>
        /// <param name="offset"></param>
        /// <param name="mediaLength">Overall lenght of the media, if known - to correctly satisfy partial EOF read</param>
        /// <returns></returns>
        public abstract byte[] Read(uint offset, uint toread, uint mediaLength);
        /// <summary>
        /// Signals read cancelling when shutting down
        /// </summary>
        public abstract void CancelPendingRead();
        /// <summary>
        /// Free allocated backing store
        /// </summary>
        public abstract void CloseStore();

        protected void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            Log.LOG(requestedLogLevel, this.logLevel, this.GetType().Name, format, args);
        }
    }
}